using FluentResults;

namespace GreenLumaPresets.Services;

public interface IGreenLumaService
{
    bool IsSteamFound { get; }
    bool IsGreenLumaInstalled();
    bool IsDeleteCacheExeInstalled();

    Result LoadAppList(Guid presetId);
    Result ClearAppList();
    Result RestartSteam();
    void DeleteSteamAppCache();

    Task<Result> InstallGreenLumaFromArchiveAsync(string pathToArchive, string? expectedSha256 = null);
    Task<Result> InstallGreenLumaFromUrlAsync(string greenLumaFilesUrl, string? expectedSha256 = null);
    Result UninstallGreenLuma();
    Task<Result> AddDefenderExclusionAsync();
}
