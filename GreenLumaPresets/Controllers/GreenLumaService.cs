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

    public bool IsGreenLumaInstalled()
    {
        return File.Exists(Path.Combine(pathToSteam, "User32.dll"));
    }

    public bool IsDeleteCacheExeInstalled()
    {
        return File.Exists(Path.Combine(pathToSteam, "DeleteSteamAppCache.exe"));
    }

    public void LoadAppList(Guid presetId)
    {
        if (string.IsNullOrEmpty(pathToSteam)) return;

        var appListPath = Path.Combine(pathToSteam, "AppList");
        ClearAppList();

        var appIds = dbContext.AppIds
            .Where(x => x.PresetId == presetId)
            .Select(a => a.Value).ToList();

        for (int i = 0; i < appIds.Count; i++)
        {
            var appFilePath = Path.Combine(appListPath, $"{i}.txt");
            File.WriteAllText(appFilePath, appIds[i].ToString());
        }
        return;
    }

    public void RestartSteam()
    {
        if (string.IsNullOrEmpty(pathToSteam)) return;

        var steamExePath = Path.Combine(pathToSteam, "Steam.exe");
        if (!File.Exists(steamExePath))
        {
            logger.LogWarning("Steam.exe not found");
            return;
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
    }

    public void ClearAppList()
    {
        var appListPath = Path.Combine(pathToSteam, "AppList");
        if (!Directory.Exists(appListPath))
        {
            Directory.CreateDirectory(appListPath);
        }

        foreach (var file in Directory.GetFiles(appListPath))
        {
            File.Delete(file);
        }
    }

    public void DeleteSteamAppCache()
    {
        if (string.IsNullOrEmpty(pathToSteam)) return;
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

    public async Task<Result> InstallGreenLuma(string greenLumaFilesUrl)
    {
        if (string.IsNullOrEmpty(pathToSteam))
        {
            logger.LogWarning("Steam path not found, cannot install GreenLuma files.");
            return Result.Fail("Steam path not found");
        }
        if (string.IsNullOrEmpty(greenLumaFilesUrl))
        {
            logger.LogWarning("GreenLuma files URL is empty, cannot proceed with installation.");
            return Result.Fail("GreenLuma files URL is empty");
        }
        var rarFilePath = Path.Combine(pathToSteam, "downloaded.rar");
        try
        {
            if (!File.Exists(rarFilePath) ||
                File.GetLastWriteTime(rarFilePath) < DateTime.Now.AddYears(-1))
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(greenLumaFilesUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await using var remoteStream = await response.Content.ReadAsStreamAsync();
                await using var localStream = new FileStream(rarFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await remoteStream.CopyToAsync(localStream);
            }

            using var archive = ArchiveFactory.Open(rarFilePath);
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
            logger.LogError($"Failed to install GreenLuma files: {ex.Message}");
            return Result.Fail($"Failed to install GreenLuma files: {ex.Message}");
        }
    }

    public Result UninstallGreenLuma()
    {
        if (string.IsNullOrEmpty(pathToSteam)) return Result.Fail("Steam path not found");

        var user32Path = Path.Combine(pathToSteam, "user32.dll");
        if (File.Exists(user32Path))
        {
            try
            {
                File.Delete(user32Path);
                logger.LogInformation("user32.dll deleted successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to delete user32.dll: {ex.Message}");
            }
        }

        var deleteCacheExePath = Path.Combine(pathToSteam, "DeleteSteamAppCache.exe");
        if (File.Exists(deleteCacheExePath))
        {
            try
            {
                File.Delete(deleteCacheExePath);
                logger.LogInformation("DeleteSteamAppCache.exe deleted successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to delete DeleteSteamAppCache.exe: {ex.Message}");
            }
        }

        logger.LogInformation("GreenLuma uninstalled successfully.");
        return Result.Ok();
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
            Console.WriteLine($"Error retrieving Steam path: {ex.Message}");
        }
        return false;
    }
}
