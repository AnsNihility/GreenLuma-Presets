using FluentResults;

namespace GreenLumaPresets.Services;

public interface ISteamService
{
    /// <summary>
    /// Gets an app and its DLC from the Steam store.
    /// </summary>
    Task<Result<SteamApp>> GetAppData(int appId);

    Task<Result<List<SteamSearchResult>>> SearchAppsByName(string name);
}
