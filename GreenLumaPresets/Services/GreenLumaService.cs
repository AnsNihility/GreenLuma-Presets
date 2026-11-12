using FluentResults;
using GreenLumaPresets.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GreenLumaPresets.Services;

public class GreenLumaService : IGreenLumaService
{
    private readonly AppDbContext dbContext;
    private readonly ILogger<GreenLumaService> logger;
    private readonly string pathToSteam;

    public GreenLumaService(AppDbContext dbContext, ILogger<GreenLumaService> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!TryGetSteamPath(out pathToSteam))
        {
            logger.LogWarning("Steam path not found");
        }
    }

    /// <summary>
    /// Uses the given Steam folder instead of the one registered by Steam.
    /// </summary>
    public GreenLumaService(AppDbContext dbContext, ILogger<GreenLumaService> logger, string pathToSteam)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.pathToSteam = pathToSteam;
    }

    /// <summary>
    /// Lists, in the Steam folder, the files extracted by the installer so uninstall removes all of them.
    /// </summary>
    private const string InstallManifestFileName = "GreenLumaPresets.installed.json";

    /// <summary>
    /// Files installed by versions that didn't write a manifest.
    /// </summary>
    private static readonly HashSet<string> KnownGreenLumaFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "user32.dll",
        "DeleteSteamAppCache.exe",
    };

    /// <summary>ERROR_CANCELLED: the user declined the UAC prompt.</summary>
    private const int ErrorCancelled = 1223;

    private const string SteamNotFoundMessage = "Steam installation not found. Make sure Steam is installed and has been launched at least once.";

    private const string SteamFolderAccessDeniedMessage = "Access to the Steam folder was denied. Close Steam and try again, or run GreenLuma Presets as administrator if the Steam folder is protected.";

    public bool IsSteamFound => !string.IsNullOrEmpty(pathToSteam) && Directory.Exists(pathToSteam);

    public bool IsGreenLumaInstalled()
    {
        return IsSteamFound && File.Exists(Path.Combine(pathToSteam, "User32.dll"));
    }

    public bool IsDeleteCacheExeInstalled()
    {
        return IsSteamFound && File.Exists(Path.Combine(pathToSteam, "DeleteSteamAppCache.exe"));
    }

    public Result LoadAppList(Guid presetId)
    {
        var clearResult = ClearAppList();
        if (clearResult.IsFailed) return clearResult;

        var appListPath = Path.Combine(pathToSteam, "AppList");

        var storedValues = dbContext.AppIds
            .Where(x => x.PresetId == presetId)
            .OrderBy(a => a.Position)
            .Select(a => a.Value).ToList();

        // Presets saved by older versions may contain placeholders, invalid values or duplicates.
        var appIds = storedValues
            .Select(value => AppIdRules.TryNormalize(value, out var normalized) ? normalized : null)
            .OfType<string>()
            .Distinct()
            .ToList();

        if (appIds.Count != storedValues.Count)
        {
            logger.LogInformation("Skipped {Count} invalid or duplicate AppIDs while loading preset {PresetId}",
                storedValues.Count - appIds.Count, presetId);
        }

        try
        {
            for (int i = 0; i < appIds.Count; i++)
            {
                var appFilePath = Path.Combine(appListPath, $"{i}.txt");
                File.WriteAllText(appFilePath, appIds[i]);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Access denied while writing the AppList");
            return Result.Fail(SteamFolderAccessDeniedMessage);
        }
        return Result.Ok();
    }

    public Result RestartSteam()
    {
        if (!IsSteamFound) return Result.Fail(SteamNotFoundMessage);

        var steamExePath = Path.Combine(pathToSteam, "Steam.exe");
        if (!File.Exists(steamExePath))
        {
            logger.LogWarning("Steam.exe not found");
            return Result.Fail("Steam.exe not found in the Steam folder");
        }

        foreach (var existingProcess in Process.GetProcessesByName("Steam"))
        {
            try
            {
                existingProcess.Kill();
                existingProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                // Typically a Steam instance running as administrator, which a non-elevated process can't close.
                logger.LogError(ex, "Failed to close the Steam process {ProcessId}", existingProcess.Id);
                return Result.Fail("Couldn't close Steam. Exit Steam manually, then use Load and launch Steam again.");
            }
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = steamExePath,
                UseShellExecute = true
            }
        };

        process.Start();
        return Result.Ok();
    }

    public Result ClearAppList()
    {
        if (!IsSteamFound) return Result.Fail(SteamNotFoundMessage);

        var appListPath = Path.Combine(pathToSteam, "AppList");
        try
        {
            Directory.CreateDirectory(appListPath);
            foreach (var file in Directory.GetFiles(appListPath))
            {
                File.Delete(file);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Access denied while clearing the AppList");
            return Result.Fail(SteamFolderAccessDeniedMessage);
        }
        return Result.Ok();
    }

    public void DeleteSteamAppCache()
    {
        if (!IsSteamFound) return;
        var deleteCacheExePath = Path.Combine(pathToSteam, "DeleteSteamAppCache.exe");
        if (!File.Exists(deleteCacheExePath))
        {
            logger.LogWarning("DeleteSteamAppCache.exe not found");
            return;
        }
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = deleteCacheExePath,
                UseShellExecute = true
            }
        };
        try
        {
            process.Start();
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to execute DeleteSteamAppCache.exe: {ex.Message}");
        }
    }

    /// <summary>
    /// Installs GreenLuma from a local archive containing the user32.dll and DeleteSteamAppCache.exe
    /// </summary>
    /// <param name="expectedSha256">Optional SHA-256 the archive must match.</param>
    public async Task<Result> InstallGreenLumaFromArchiveAsync(string pathToArchive, string? expectedSha256 = null)
    {
        if (!IsSteamFound)
        {
            logger.LogWarning("Steam path not found, cannot install GreenLuma files.");
            return Result.Fail(SteamNotFoundMessage);
        }
        if (string.IsNullOrEmpty(pathToArchive) || !File.Exists(pathToArchive))
        {
            logger.LogWarning("Archive path is empty or file does not exist, cannot proceed with installation.");
            return Result.Fail("Archive path is empty or file does not exist");
        }

        if (!await MatchesSha256Async(pathToArchive, expectedSha256))
        {
            return Result.Fail("The GreenLuma archive doesn't match the configured SHA-256 checksum. It may be corrupted or tampered with.");
        }

        return await Task.Run(() => ExtractToSteamDirectory(pathToArchive));
    }

    /// <summary>
    /// Downloads the GreenLuma archive (cached per URL for a year) and installs it.
    /// </summary>
    /// <param name="expectedSha256">Optional SHA-256 the archive must match.</param>
    public async Task<Result> InstallGreenLumaFromUrlAsync(string greenLumaFilesUrl, string? expectedSha256 = null)
    {
        if (!IsSteamFound)
        {
            logger.LogWarning("Steam path not found, cannot install GreenLuma files.");
            return Result.Fail(SteamNotFoundMessage);
        }
        if (string.IsNullOrEmpty(greenLumaFilesUrl))
        {
            logger.LogWarning("GreenLuma files URL is empty, cannot proceed with installation.");
            return Result.Fail("GreenLuma files URL is empty");
        }

        DeleteLegacyDownloadCache();

        var rarFilePath = GetDownloadCachePath(greenLumaFilesUrl);
        try
        {
            var isCacheUsable = File.Exists(rarFilePath)
                && File.GetLastWriteTime(rarFilePath) >= DateTime.Now.AddYears(-1)
                && await MatchesSha256Async(rarFilePath, expectedSha256);

            if (!isCacheUsable)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(rarFilePath)!);
                var partialFilePath = rarFilePath + ".part";

                using (var client = new HttpClient())
                using (var response = await client.GetAsync(greenLumaFilesUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    await using var remoteStream = await response.Content.ReadAsStreamAsync();
                    await using var localStream = new FileStream(partialFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await remoteStream.CopyToAsync(localStream);
                }

                if (!await MatchesSha256Async(partialFilePath, expectedSha256))
                {
                    File.Delete(partialFilePath);
                    logger.LogError("Downloaded GreenLuma archive from {Url} doesn't match the configured SHA-256", greenLumaFilesUrl);
                    return Result.Fail("The downloaded GreenLuma archive doesn't match the configured SHA-256 checksum. It may be corrupted or tampered with.");
                }

                File.Move(partialFilePath, rarFilePath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download GreenLuma from {Url}", greenLumaFilesUrl);
            return Result.Fail($"Failed to download GreenLuma: {ex.Message}");
        }

        return await Task.Run(() => ExtractToSteamDirectory(rarFilePath));
    }

    private static string GetDownloadCachePath(string url)
    {
        // One cache entry per URL so changing the URL in the settings triggers a new download.
        var urlHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        return Path.Combine(AppPaths.CacheDirectory, $"greenluma-{urlHash}.rar");
    }

    /// <summary>
    /// Older versions cached the archive in the Steam folder.
    /// </summary>
    private void DeleteLegacyDownloadCache()
    {
        var legacyPath = Path.Combine(pathToSteam, "downloaded.rar");
        try
        {
            if (File.Exists(legacyPath)) File.Delete(legacyPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Couldn't delete the legacy download cache {Path}", legacyPath);
        }
    }

    private static async Task<bool> MatchesSha256Async(string filePath, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return true;

        await using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private Result ExtractToSteamDirectory(string archivePath)
    {
        var installedFiles = ReadInstallManifest();
        try
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory || string.IsNullOrEmpty(entry.Key)) continue;

                var relativePath = NormalizeRelativePath(entry.Key);
                var fullPath = Path.GetFullPath(Path.Combine(pathToSteam, relativePath));
                if (!IsInsideSteamDirectory(fullPath))
                {
                    logger.LogWarning("Skipped archive entry {Entry} pointing outside the Steam folder", entry.Key);
                    continue;
                }

                var existedBefore = File.Exists(fullPath);
                entry.WriteToDirectory(pathToSteam, new ExtractionOptions()
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                });

                // Only track files GreenLuma owns: never delete a Steam file the archive happened to overwrite.
                if (!existedBefore || installedFiles.Contains(relativePath) || KnownGreenLumaFiles.Contains(relativePath))
                {
                    installedFiles.Add(relativePath);
                }
                else
                {
                    logger.LogWarning("{File} already existed and was overwritten; it won't be removed on uninstall", relativePath);
                }
            }

            logger.LogInformation("GreenLuma files installed successfully.");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install GreenLuma files");
            return Result.Fail($"Failed to install GreenLuma files: {ex.Message}");
        }
        finally
        {
            // Saved even after a partial extraction so uninstall can clean it up.
            WriteInstallManifest(installedFiles);
        }
    }

    /// <summary>
    /// Removes every file extracted by the installer, plus the files installed by versions without a manifest.
    /// </summary>
    public Result UninstallGreenLuma()
    {
        if (!IsSteamFound) return Result.Fail(SteamNotFoundMessage);

        var remainingFiles = ReadInstallManifest();
        remainingFiles.UnionWith(KnownGreenLumaFiles);

        var errors = new List<string>();
        foreach (var relativePath in remainingFiles.ToList())
        {
            var fullPath = Path.GetFullPath(Path.Combine(pathToSteam, relativePath));
            if (!IsInsideSteamDirectory(fullPath))
            {
                remainingFiles.Remove(relativePath);
                continue;
            }

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    logger.LogInformation("{File} deleted successfully.", relativePath);
                }
                remainingFiles.Remove(relativePath);
                DeleteEmptyParentDirectories(fullPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete {File}", relativePath);
                errors.Add($"Couldn't delete {relativePath}: {ex.Message}");
            }
        }

        // Keep only what couldn't be deleted, so a later uninstall retries it.
        WriteInstallManifest(remainingFiles);

        if (errors.Count > 0)
        {
            errors.Add("Close Steam and try again.");
            return Result.Fail(string.Join(Environment.NewLine, errors));
        }

        logger.LogInformation("GreenLuma uninstalled successfully.");
        return Result.Ok();
    }

    private string InstallManifestPath => Path.Combine(pathToSteam, InstallManifestFileName);

    private HashSet<string> ReadInstallManifest()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(InstallManifestPath))
            {
                var stored = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(InstallManifestPath)) ?? [];
                files.UnionWith(stored.Select(NormalizeRelativePath));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Couldn't read the GreenLuma install manifest");
        }
        return files;
    }

    private void WriteInstallManifest(HashSet<string> files)
    {
        try
        {
            if (files.Count == 0)
            {
                if (File.Exists(InstallManifestPath)) File.Delete(InstallManifestPath);
                return;
            }

            var json = JsonSerializer.Serialize(
                files.Order(StringComparer.OrdinalIgnoreCase).ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(InstallManifestPath, json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Couldn't write the GreenLuma install manifest");
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
    }

    private bool IsInsideSteamDirectory(string fullPath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(pathToSteam)) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private void DeleteEmptyParentDirectories(string filePath)
    {
        var steamRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(pathToSteam));
        var directory = Path.GetDirectoryName(filePath);
        while (directory != null
            && !string.Equals(Path.TrimEndingDirectorySeparator(directory), steamRoot, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(directory)
            && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    /// <summary>
    /// Adds the Steam folder to the Windows Defender exclusions.
    /// The app runs without administrator rights, so only this command is elevated (UAC prompt).
    /// </summary>
    public async Task<Result> AddDefenderExclusionAsync()
    {
        if (!IsSteamFound) return Result.Fail(SteamNotFoundMessage);

        // Single quotes are the only character to escape inside a PowerShell single-quoted string.
        var escapedPath = pathToSteam.Replace("'", "''");
        var command = $"try {{ Add-MpPreference -ExclusionPath '{escapedPath}' -ErrorAction Stop; exit 0 }} catch {{ exit 1 }}";

        // Elevation requires ShellExecute, which can't redirect output: the exit code reports the outcome.
        var startInfo = new ProcessStartInfo("powershell")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-Command", command })
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Couldn't start PowerShell");
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                logger.LogError("Add-MpPreference failed with exit code {ExitCode}", process.ExitCode);
                return Result.Fail("Windows Defender refused the exclusion. It may be managed by another antivirus or by your organization.");
            }

            logger.LogInformation("Windows Defender exclusion added for {Path}", pathToSteam);
            return Result.Ok();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            logger.LogInformation("Windows Defender exclusion cancelled at the UAC prompt");
            return Result.Fail("The administrator permission was declined, so the exclusion wasn't added.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add the Windows Defender exclusion");
            return Result.Fail($"Failed to add the Windows Defender exclusion: {ex.Message}");
        }
    }

    private bool TryGetSteamPath(out string path)
    {
        path = string.Empty;
        try
        {
            var registryPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");

            if (registryPath == null) return false;

            using (RegistryKey key = registryPath)
            {
                if (key == null || key.GetValue("SteamPath") is not string foundPath) return false;
                path = foundPath;
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving Steam path");
        }
        return false;
    }
}
