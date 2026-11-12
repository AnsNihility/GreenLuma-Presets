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
}
