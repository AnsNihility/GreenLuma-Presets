using FluentResults;
using GreenLumaPresets.Models;

namespace GreenLumaPresets.Services;

public interface IPresetsService
{
    List<Preset> GetPresets();
    List<AppId> GetAppIdsForPreset(Guid presetId);
    Dictionary<Preset, List<AppId>> GetPresetsWithAppIds();

    Preset AddPreset(string name);
    void RemovePreset(Guid id);
    string? GetPresetName(Guid id);
    Result UpdatePreset(Guid id, string name);

    AppId? AddAppId(Guid presetId, string appId);

    /// <summary>
    /// Adds the valid App IDs to the preset, skipping invalid values and App IDs already present.
    /// </summary>
    /// <returns>The App IDs actually added.</returns>
    List<AppId> AddAppIds(Guid presetId, IEnumerable<string> appIds);

    string? GetAppIdValue(Guid appId);
    Result<string> UpdateAppId(Guid presetId, Guid appId, string value);
    void RemoveAppId(Guid presetId, Guid idToDelete);

    /// <summary>
    /// Picks the AppID whose icon represents the preset; null goes back to the automatic icon.
    /// </summary>
    Result SetPresetIcon(Guid presetId, Guid? appId);

    string? GetAppIdName(Guid appId);

    /// <summary>
    /// Renames an AppID; a blank name removes it.
    /// </summary>
    /// <returns>The stored name.</returns>
    Result<string?> UpdateAppIdName(Guid appId, string? name);

    /// <summary>
    /// Applies Steam details to every AppID with a matching value, in all presets.
    /// Names are only filled when missing, so names typed by the user are kept; icons are replaced.
    /// </summary>
    /// <returns>The App IDs that changed.</returns>
    List<AppId> ApplyAppDetails(IReadOnlyDictionary<uint, SteamAppDetails> details);
}
