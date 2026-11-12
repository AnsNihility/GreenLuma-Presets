using FluentResults;

namespace GreenLumaPresets.Services;

public interface ISteamService
{
    /// <summary>
    /// Gets an app and its DLC from the Steam store.
    /// </summary>
    Task<Result<SteamApp>> GetAppData(int appId);

    Task<Result<List<SteamSearchResult>>> SearchAppsByName(string name);

    /// <summary>
    /// Gets the names and icons of many apps in a few requests. Apps unknown to Steam are left out.
    /// </summary>
    Task<Result<Dictionary<uint, SteamAppDetails>>> GetAppDetails(IEnumerable<uint> appIds);
}
