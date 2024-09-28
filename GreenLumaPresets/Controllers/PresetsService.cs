using GreenLumaPresets.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        return dbContext.Presets.ToDictionary(
                p => p, 
                p => dbContext.AppIds.Where(a => a.PresetId == p.Id).ToList());
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

    public void UpdatePreset(Guid id, string name)
    {
        var preset = dbContext.Presets.FirstOrDefault(p => p.Id == id);
        if (preset == null) return;

        preset.Name = name;
        dbContext.SaveChanges();
    }

    public AppId? AddAppId(Guid presetId, string appId)
    {
        if (dbContext.Presets.FirstOrDefault(p => p.Id == presetId) == null) return null;

        var newAppId = new AppId { Id = Guid.NewGuid(), PresetId = presetId, Value = appId };

        dbContext.AppIds.Add(newAppId);
        dbContext.SaveChanges();

        return newAppId;
    }

    public List<AppId> AddAppIds(Guid presetId, IEnumerable<string> appIds)
    {
        if (dbContext.Presets.FirstOrDefault(p => p.Id == presetId) == null) return [];

        var newAppIds = appIds.Select(x => new AppId { Id = Guid.NewGuid(), PresetId = presetId, Value = x }).ToList();

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

    public void UpdateAppId(Guid presetId, Guid appId, string value)
    {
        if (dbContext.Presets.FirstOrDefault(p => p.Id == presetId) == null) return;

        var appIdToUpdate = dbContext.AppIds.FirstOrDefault(a => a.Id == appId);
        if (appIdToUpdate == null) return;

        appIdToUpdate.Value = value;
        dbContext.SaveChanges();
    }
}
