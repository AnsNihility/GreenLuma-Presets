using FluentResults;
using GreenLumaPresets.Models;

namespace GreenLumaPresets.Services;

public class PresetsService : IPresetsService
{
    private readonly AppDbContext dbContext;

    public PresetsService(AppDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public List<Preset> GetPresets()
    {
        return dbContext.Presets.ToList();
    }
     
    public List<AppId> GetAppIdsForPreset(Guid presetId)
    {
        return dbContext.AppIds
            .Where(a => a.PresetId == presetId)
            .OrderBy(a => a.Position)
            .ToList();
    }

    public Dictionary<Preset, List<AppId>> GetPresetsWithAppIds()
    {
        // Two queries in total instead of one per preset.
        var appIdsByPreset = dbContext.AppIds
            .OrderBy(a => a.Position)
            .ToList()
            .ToLookup(a => a.PresetId);

        return dbContext.Presets
            .ToList()
            .ToDictionary(p => p, p => appIdsByPreset[p.Id].ToList());
    }

    public Preset AddPreset(string name)
    {
        var preset = new Preset { Id = Guid.NewGuid(), Name = name };
        dbContext.Presets.Add(preset);

        dbContext.SaveChanges();
        return preset;
    }

    public void RemovePreset(Guid id)
    {
        var preset = dbContext.Presets.FirstOrDefault(p => p.Id == id);
        if (preset == null) return;

        // Its AppIDs are removed by the cascading foreign key.
        dbContext.Presets.Remove(preset);
        dbContext.SaveChanges();
    }

    public string? GetPresetName(Guid id)
    {
        return dbContext.Presets.FirstOrDefault(p => p.Id == id)?.Name;
    }

    public Result UpdatePreset(Guid id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Result.Fail("The preset name cannot be empty");

        var preset = dbContext.Presets.FirstOrDefault(p => p.Id == id);
        if (preset == null) return Result.Fail("This preset no longer exists");

        preset.Name = name.Trim();
        dbContext.SaveChanges();
        return Result.Ok();
    }

    public AppId? AddAppId(Guid presetId, string appId)
    {
        if (dbContext.Presets.FirstOrDefault(p => p.Id == presetId) == null) return null;

        var newAppId = new AppId { Id = Guid.NewGuid(), PresetId = presetId, Value = appId, Position = GetNextPosition(presetId) };

        dbContext.AppIds.Add(newAppId);
        dbContext.SaveChanges();

        return newAppId;
    }

    /// <summary>
    /// Adds the valid App IDs to the preset, skipping invalid values and App IDs already present.
    /// </summary>
    /// <returns>The App IDs actually added.</returns>
    public List<AppId> AddAppIds(Guid presetId, IEnumerable<string> appIds)
    {
        if (dbContext.Presets.FirstOrDefault(p => p.Id == presetId) == null) return [];

        var knownValues = dbContext.AppIds
            .Where(a => a.PresetId == presetId)
            .Select(a => a.Value)
            .ToHashSet();

        var nextPosition = GetNextPosition(presetId);
        var newAppIds = new List<AppId>();
        foreach (var value in appIds)
        {
            if (!AppIdRules.TryNormalize(value, out var normalized)) continue;
            if (!knownValues.Add(normalized)) continue;

            newAppIds.Add(new AppId { Id = Guid.NewGuid(), PresetId = presetId, Value = normalized, Position = nextPosition++ });
        }

        dbContext.AppIds.AddRange(newAppIds);
        dbContext.SaveChanges();

        return newAppIds;
    }

    private int GetNextPosition(Guid presetId)
    {
        return (dbContext.AppIds
            .Where(a => a.PresetId == presetId)
            .Max(a => (int?)a.Position) ?? 0) + 1;
    }

    public void RemoveAppId(Guid presetId, Guid idToDelete)
    {
        if (dbContext.Presets.FirstOrDefault(p => p.Id == presetId) == null) return;

        var appId = dbContext.AppIds.FirstOrDefault(a => a.Id == idToDelete);
        if (appId == null) return;

        dbContext.AppIds.Remove(appId);
        foreach (var preset in dbContext.Presets.Where(p => p.IconAppId == idToDelete))
        {
            preset.IconAppId = null;
        }
        dbContext.SaveChanges();
    }

    public Result SetPresetIcon(Guid presetId, Guid? appId)
    {
        var preset = dbContext.Presets.FirstOrDefault(p => p.Id == presetId);
        if (preset == null) return Result.Fail("This preset no longer exists");

        if (appId != null && !dbContext.AppIds.Any(a => a.Id == appId && a.PresetId == presetId))
            return Result.Fail("This AppID is not in the preset");

        preset.IconAppId = appId;
        dbContext.SaveChanges();
        return Result.Ok();
    }

    public string? GetAppIdName(Guid appId)
    {
        return dbContext.AppIds.FirstOrDefault(a => a.Id == appId)?.Name;
    }

    public Result<string?> UpdateAppIdName(Guid appId, string? name)
    {
        var appIdToUpdate = dbContext.AppIds.FirstOrDefault(a => a.Id == appId);
        if (appIdToUpdate == null) return Result.Fail("This AppID no longer exists");

        appIdToUpdate.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        dbContext.SaveChanges();
        return Result.Ok(appIdToUpdate.Name);
    }

    /// <summary>
    /// Applies Steam details to every AppID with a matching value, in all presets.
    /// Names are only filled when missing, so names typed by the user are kept; icons are replaced.
    /// </summary>
    /// <returns>The App IDs that changed.</returns>
    public List<AppId> ApplyAppDetails(IReadOnlyDictionary<uint, SteamAppDetails> details)
    {
        if (details.Count == 0) return [];

        var values = details.Keys.Select(k => k.ToString()).ToList();
        var changed = new List<AppId>();
        foreach (var appId in dbContext.AppIds.Where(a => values.Contains(a.Value)).ToList())
        {
            var detail = details[uint.Parse(appId.Value)];
            var name = appId.Name ?? detail.Name;
            if (name == appId.Name && detail.IconUrl == appId.IconUrl) continue;

            appId.Name = name;
            appId.IconUrl = detail.IconUrl;
            changed.Add(appId);
        }

        dbContext.SaveChanges();
        return changed;
    }

    public string? GetAppIdValue(Guid appId)
    {
        return dbContext.AppIds.FirstOrDefault(a => a.Id == appId)?.Value;
    }

    public Result<string> UpdateAppId(Guid presetId, Guid appId, string value)
    {
        if (!AppIdRules.TryNormalize(value, out var normalized))
            return Result.Fail($"\"{value}\" is not a valid AppID. An AppID is a positive number, e.g. 620.");

        if (dbContext.Presets.FirstOrDefault(p => p.Id == presetId) == null)
            return Result.Fail("This preset no longer exists");

        var appIdToUpdate = dbContext.AppIds.FirstOrDefault(a => a.Id == appId);
        if (appIdToUpdate == null) return Result.Fail("This AppID no longer exists");

        if (dbContext.AppIds.Any(a => a.PresetId == presetId && a.Id != appId && a.Value == normalized))
            return Result.Fail($"AppID {normalized} is already in this preset");

        if (appIdToUpdate.Value != normalized)
        {
            // The name and icon described the previous app.
            appIdToUpdate.Name = null;
            appIdToUpdate.IconUrl = null;
        }
        appIdToUpdate.Value = normalized;
        dbContext.SaveChanges();
        return Result.Ok(normalized);
    }
}
