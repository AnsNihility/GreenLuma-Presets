using GreenLumaPresets.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

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

    public void LoadAppList()
    {
        if (string.IsNullOrEmpty(pathToSteam)) return;

        var appListPath = Path.Combine(pathToSteam, "AppList");
        ClearAppList();

        var appIds = dbContext.AppIds.Select(a => a.Value).ToList();
        for (int i = 0; i < appIds.Count; i++)
        {
            var appFilePath = Path.Combine(appListPath, $"{i}.txt");
            File.WriteAllText(appFilePath, appIds[i].ToString());
        }
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
                existingProcess.Kill();
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

    private void ClearAppList()
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

    private bool TryGetSteamPath(out string path)
    {
        path = string.Empty;
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
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
