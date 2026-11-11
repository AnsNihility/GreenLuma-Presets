using GreenLumaPresets.Services;
using GreenLumaPresets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;

namespace GreenLumaPresets.Tests;

public sealed class GreenLumaServiceInstallTests : IDisposable
{
    private readonly string workDirectory;
    private readonly string steamDirectory;
    private readonly AppDbContext dbContext;
    private readonly GreenLumaService service;

    public GreenLumaServiceInstallTests()
    {
        workDirectory = Path.Combine(Path.GetTempPath(), "GreenLumaPresetsTests", Guid.NewGuid().ToString("N"));
        steamDirectory = Path.Combine(workDirectory, "Steam");
        Directory.CreateDirectory(steamDirectory);

        // The database is not used by install/uninstall; the connection is never opened.
        dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        service = new GreenLumaService(dbContext, NullLogger<GreenLumaService>.Instance, steamDirectory);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        Directory.Delete(workDirectory, recursive: true);
    }

    [Fact]
    public async Task Uninstall_RemovesEveryExtractedFileAndFolder()
    {
        var archive = CreateArchive(
            "user32.dll",
            "DeleteSteamAppCache.exe",
            "GreenLuma/GreenLumaSettings.exe",
            "GreenLuma/nested/config.ini");

        Assert.True((await service.InstallGreenLumaFromArchiveAsync(archive)).IsSuccess);
        Assert.True(service.IsGreenLumaInstalled());
        Assert.True(File.Exists(Path.Combine(steamDirectory, "GreenLuma", "nested", "config.ini")));

        Assert.True(service.UninstallGreenLuma().IsSuccess);

        Assert.False(service.IsGreenLumaInstalled());
        Assert.Empty(Directory.EnumerateFileSystemEntries(steamDirectory));
    }

    [Fact]
    public async Task Uninstall_KeepsSteamFilesOverwrittenByTheArchive()
    {
        var steamFile = Path.Combine(steamDirectory, "steam.dll");
        File.WriteAllText(steamFile, "original");
        var archive = CreateArchive("user32.dll", "steam.dll");

        await service.InstallGreenLumaFromArchiveAsync(archive);
        service.UninstallGreenLuma();

        Assert.True(File.Exists(steamFile));
        Assert.False(File.Exists(Path.Combine(steamDirectory, "user32.dll")));
    }

    [Fact]
    public async Task Reinstall_KeepsTrackingFilesFromThePreviousInstall()
    {
        var archive = CreateArchive("user32.dll", "GreenLuma/settings.ini");

        await service.InstallGreenLumaFromArchiveAsync(archive);
        // Second install: the files now exist but belong to GreenLuma.
        await service.InstallGreenLumaFromArchiveAsync(archive);
        service.UninstallGreenLuma();

        Assert.Empty(Directory.EnumerateFileSystemEntries(steamDirectory));
    }

    [Fact]
    public void Uninstall_WithoutManifest_RemovesFilesInstalledByOlderVersions()
    {
        File.WriteAllText(Path.Combine(steamDirectory, "User32.dll"), "");
        File.WriteAllText(Path.Combine(steamDirectory, "DeleteSteamAppCache.exe"), "");
        File.WriteAllText(Path.Combine(steamDirectory, "steam.exe"), "");

        Assert.True(service.UninstallGreenLuma().IsSuccess);

        Assert.Equal(["steam.exe"], Directory.EnumerateFiles(steamDirectory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Install_SkipsEntriesOutsideTheSteamFolder()
    {
        var archive = CreateArchive("user32.dll", "../outside.txt");

        await service.InstallGreenLumaFromArchiveAsync(archive);

        Assert.False(File.Exists(Path.Combine(workDirectory, "outside.txt")));
        Assert.True(File.Exists(Path.Combine(steamDirectory, "user32.dll")));
    }

    [Fact]
    public async Task Install_RejectsArchiveWithWrongChecksum()
    {
        var archive = CreateArchive("user32.dll");

        var result = await service.InstallGreenLumaFromArchiveAsync(archive, expectedSha256: new string('0', 64));

        Assert.True(result.IsFailed);
        Assert.False(File.Exists(Path.Combine(steamDirectory, "user32.dll")));
    }

    private string CreateArchive(params string[] entries)
    {
        var path = Path.Combine(workDirectory, $"{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
            writer.Write($"content of {entry}");
        }
        return path;
    }
}
