using FluentResults;
using GreenLumaPresets.Models;

namespace GreenLumaPresets.Controllers;

public class PresetsService
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
        return dbContext.AppIds.Where(a => a.PresetId == presetId).ToList();
    }

    public Dictionary<Preset, List<AppId>> GetPresetsWithAppIds()
    {
        // Two queries in total instead of one per preset.
        var appIdsByPreset = dbContext.AppIds
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

        dbContext.Presets.Remove(preset);

        var appIds = dbContext.AppIds.Where(a => a.PresetId == id).ToList();
        if (appIds.Count > 0) 
            dbContext.RemoveRange(appIds);

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

        var newAppId = new AppId { Id = Guid.NewGuid(), PresetId = presetId, Value = appId };

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

        var newAppIds = new List<AppId>();
        foreach (var value in appIds)
        {
            if (!AppIdRules.TryNormalize(value, out var normalized)) continue;
            if (!knownValues.Add(normalized)) continue;

            newAppIds.Add(new AppId { Id = Guid.NewGuid(), PresetId = presetId, Value = normalized });
        }

        dbContext.AppIds.AddRange(newAppIds);
        dbContext.SaveChanges();

        return newAppIds;
    }

    public void RemoveAppId(Guid presetId, Guid idToDelete)
    {
        if (dbContext.Presets.FirstOrDefault(p => p.Id == presetId) == null) return;

        var appId = dbContext.AppIds.FirstOrDefault(a => a.Id == idToDelete);
        if (appId == null) return;

        dbContext.AppIds.Remove(appId);
        dbContext.SaveChanges();
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

        appIdToUpdate.Value = normalized;
        dbContext.SaveChanges();
        return Result.Ok(normalized);
    }
}
