using FluentResults;
using GreenLumaPresets.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace GreenLumaPresets.Controllers;

public class GreenLumaService
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

    private const string SteamNotFoundMessage = "Steam installation not found. Make sure Steam is installed and has been launched at least once.";

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

        for (int i = 0; i < appIds.Count; i++)
        {
            var appFilePath = Path.Combine(appListPath, $"{i}.txt");
            File.WriteAllText(appFilePath, appIds[i]);
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

        var steamProcesses = Process.GetProcessesByName("Steam");
        if (steamProcesses.Length > 0)
        {
            foreach (var existingProcess in steamProcesses)
            {
                try
                {
                    existingProcess.Kill();
                    existingProcess.WaitForExit();
                }
                catch (Exception ex)
                {
                    logger.LogError($"Failed to kill Steam process: {ex.Message}");
                }
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
        if (!Directory.Exists(appListPath))
        {
            Directory.CreateDirectory(appListPath);
        }

        foreach (var file in Directory.GetFiles(appListPath))
        {
            File.Delete(file);
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
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GreenLumaPresets",
            "cache",
            $"greenluma-{urlHash}.rar");
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
        try
        {
            using var archive = ArchiveFactory.Open(archivePath);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;
                entry.WriteToDirectory(pathToSteam, new ExtractionOptions()
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                });
            }

            logger.LogInformation("GreenLuma files installed successfully.");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install GreenLuma files");
            return Result.Fail($"Failed to install GreenLuma files: {ex.Message}");
        }
    }

    public Result UninstallGreenLuma()
    {
        if (!IsSteamFound) return Result.Fail(SteamNotFoundMessage);

        var errors = new List<string>();
        foreach (var fileName in new[] { "user32.dll", "DeleteSteamAppCache.exe" })
        {
            var filePath = Path.Combine(pathToSteam, fileName);
            if (!File.Exists(filePath)) continue;

            try
            {
                File.Delete(filePath);
                logger.LogInformation("{FileName} deleted successfully.", fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete {FileName}", fileName);
                errors.Add($"Couldn't delete {fileName}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            errors.Add("Close Steam and try again.");
            return Result.Fail(string.Join(Environment.NewLine, errors));
        }

        logger.LogInformation("GreenLuma uninstalled successfully.");
        return Result.Ok();
    }

    /// <summary>
    /// Adds the Steam folder to the Windows Defender exclusions. Requires administrator rights.
    /// </summary>
    public async Task<Result> AddDefenderExclusionAsync()
    {
        if (!IsSteamFound) return Result.Fail(SteamNotFoundMessage);

        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            return Result.Fail("Adding a Windows Defender exclusion requires running GreenLuma Presets as administrator.");
        }

        // Single quotes are the only character to escape inside a PowerShell single-quoted string.
        var escapedPath = pathToSteam.Replace("'", "''");
        var startInfo = new ProcessStartInfo("powershell")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", $"Add-MpPreference -ExclusionPath '{escapedPath}'" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Couldn't start PowerShell");
            var errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(errorOutput))
            {
                logger.LogError("Add-MpPreference failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
                return Result.Fail("Windows Defender refused the exclusion. It may be managed by another antivirus or by your organization.");
            }

            logger.LogInformation("Windows Defender exclusion added for {Path}", pathToSteam);
            return Result.Ok();
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
