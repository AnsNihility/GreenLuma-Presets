using GreenLumaPresets.Services;
using GreenLumaPresets.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GreenLumaPresets.Tests;

public sealed class PresetsServiceTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly AppDbContext dbContext;
    private readonly PresetsService service;

    public PresetsServiceTests()
    {
        // An in-memory database lives as long as its connection stays open.
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        dbContext = new AppDbContext(options);
        dbContext.Database.Migrate();

        service = new PresetsService(dbContext);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
    }

    [Fact]
    public void AddAppIds_SkipsInvalidAndDuplicateValues()
    {
        var preset = service.AddPreset("Test");
        service.AddAppIds(preset.Id, ["620"]);

        var added = service.AddAppIds(preset.Id, ["620", " 400 ", "abc", "0", "400", "", "00730"]);

        Assert.Equal(["400", "730"], added.Select(a => a.Value));
        Assert.Equal(["620", "400", "730"], service.GetAppIdsForPreset(preset.Id).Select(a => a.Value));
    }

    [Fact]
    public void AddAppIds_ReturnsPersistedIds()
    {
        var preset = service.AddPreset("Test");

        var added = service.AddAppIds(preset.Id, ["620"]).Single();

        Assert.Equal(added.Id, service.GetAppIdsForPreset(preset.Id).Single().Id);
    }

    [Fact]
    public void AddAppIds_UnknownPreset_ReturnsEmpty()
    {
        Assert.Empty(service.AddAppIds(Guid.NewGuid(), ["620"]));
    }

    [Fact]
    public void UpdateAppId_NormalizesValidValue()
    {
        var preset = service.AddPreset("Test");
        var appId = service.AddAppId(preset.Id, AppIdRules.Placeholder)!;

        var result = service.UpdateAppId(preset.Id, appId.Id, " 0620 ");

        Assert.True(result.IsSuccess);
        Assert.Equal("620", result.Value);
        Assert.Equal("620", service.GetAppIdValue(appId.Id));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("")]
    public void UpdateAppId_RejectsInvalidValue(string value)
    {
        var preset = service.AddPreset("Test");
        var appId = service.AddAppIds(preset.Id, ["620"]).Single();

        var result = service.UpdateAppId(preset.Id, appId.Id, value);

        Assert.True(result.IsFailed);
        Assert.Equal("620", service.GetAppIdValue(appId.Id));
    }

    [Fact]
    public void UpdateAppId_RejectsDuplicateInSamePreset()
    {
        var preset = service.AddPreset("Test");
        var appIds = service.AddAppIds(preset.Id, ["620", "400"]);

        var result = service.UpdateAppId(preset.Id, appIds[1].Id, "620");

        Assert.True(result.IsFailed);
        Assert.Equal("400", service.GetAppIdValue(appIds[1].Id));
    }

    [Fact]
    public void UpdatePreset_RejectsEmptyName()
    {
        var preset = service.AddPreset("Test");

        Assert.True(service.UpdatePreset(preset.Id, "   ").IsFailed);
        Assert.Equal("Test", service.GetPresetName(preset.Id));
    }

    [Fact]
    public void RemovePreset_RemovesItsAppIds()
    {
        var kept = service.AddPreset("Kept");
        var removed = service.AddPreset("Removed");
        service.AddAppIds(kept.Id, ["620"]);
        service.AddAppIds(removed.Id, ["400", "730"]);

        service.RemovePreset(removed.Id);

        Assert.Single(dbContext.AppIds);
        Assert.Equal(["Kept"], service.GetPresets().Select(p => p.Name));
    }

    [Fact]
    public void Database_RejectsAppIdWithoutPreset()
    {
        dbContext.AppIds.Add(new AppId { Id = Guid.NewGuid(), PresetId = Guid.NewGuid(), Value = "620" });

        Assert.Throws<DbUpdateException>(() => dbContext.SaveChanges());
    }

    [Fact]
    public void Database_CascadesPresetDeletionToItsAppIds()
    {
        var preset = service.AddPreset("Test");
        service.AddAppIds(preset.Id, ["620", "400"]);
        dbContext.ChangeTracker.Clear();

        // Bypass EF's own cascade to check the database constraint.
        dbContext.Database.ExecuteSql($"DELETE FROM Presets WHERE Id = {preset.Id}");

        Assert.Empty(dbContext.AppIds);
    }

    [Fact]
    public void GetPresetsWithAppIds_GroupsAppIdsByPreset()
    {
        var first = service.AddPreset("First");
        var second = service.AddPreset("Second");
        var empty = service.AddPreset("Empty");
        service.AddAppIds(first.Id, ["620", "400"]);
        service.AddAppIds(second.Id, ["730"]);

        var presets = service.GetPresetsWithAppIds().ToDictionary(x => x.Key.Name, x => x.Value);

        Assert.Equal(["620", "400"], presets["First"].Select(a => a.Value));
        Assert.Equal(["730"], presets["Second"].Select(a => a.Value));
        Assert.Empty(presets["Empty"]);
    }

    [Fact]
    public void ApplyAppDetails_FillsMissingNamesKeepsTypedOnesAndReplacesIcons()
    {
        var first = service.AddPreset("First");
        var second = service.AddPreset("Second");
        var typed = service.AddAppIds(first.Id, ["620", "400"])[0];
        service.UpdateAppIdName(typed.Id, "My Portal 2");
        service.AddAppIds(second.Id, ["620", "730"]);

        var changed = service.ApplyAppDetails(new Dictionary<uint, SteamAppDetails>
        {
            [620] = new("Portal 2", "https://icons/620.jpg"),
            [400] = new("Portal", null),
        });

        Assert.Equal(3, changed.Count);
        var all = dbContext.AppIds.AsNoTracking().ToList();
        Assert.Equal(["My Portal 2", "Portal 2"], all.Where(a => a.Value == "620").Select(a => a.Name).Order());
        Assert.All(all.Where(a => a.Value == "620"), a => Assert.Equal("https://icons/620.jpg", a.IconUrl));
        Assert.Equal("Portal", all.Single(a => a.Value == "400").Name);
        Assert.Null(all.Single(a => a.Value == "730").Name);
    }

    [Fact]
    public void ApplyAppDetails_NothingNew_ReportsNoChange()
    {
        var preset = service.AddPreset("Test");
        service.AddAppIds(preset.Id, ["620"]);
        var details = new Dictionary<uint, SteamAppDetails> { [620] = new("Portal 2", "https://icons/620.jpg") };
        service.ApplyAppDetails(details);

        Assert.Empty(service.ApplyAppDetails(details));
    }

    [Fact]
    public void UpdateAppIdName_TrimsAndClearsBlankNames()
    {
        var preset = service.AddPreset("Test");
        var appId = service.AddAppIds(preset.Id, ["620"])[0];

        Assert.Equal("Portal 2", service.UpdateAppIdName(appId.Id, "  Portal 2 ").Value);
        Assert.Null(service.UpdateAppIdName(appId.Id, "   ").Value);
        Assert.Null(service.GetAppIdName(appId.Id));
    }

    [Fact]
    public void UpdateAppId_NewValue_ClearsTheNameAndIcon()
    {
        var preset = service.AddPreset("Test");
        var appId = service.AddAppIds(preset.Id, ["620"])[0];
        service.ApplyAppDetails(new Dictionary<uint, SteamAppDetails> { [620] = new("Portal 2", "https://icons/620.jpg") });

        service.UpdateAppId(preset.Id, appId.Id, "620");
        Assert.Equal("Portal 2", service.GetAppIdName(appId.Id));

        service.UpdateAppId(preset.Id, appId.Id, "400");
        var stored = dbContext.AppIds.AsNoTracking().Single();
        Assert.Null(stored.Name);
        Assert.Null(stored.IconUrl);
    }

    [Fact]
    public void SetPresetIcon_OnlyAcceptsAnAppIdOfThePreset()
    {
        var preset = service.AddPreset("Test");
        var other = service.AddPreset("Other");
        var own = service.AddAppIds(preset.Id, ["620"])[0];
        var foreign = service.AddAppIds(other.Id, ["400"])[0];

        Assert.True(service.SetPresetIcon(preset.Id, own.Id).IsSuccess);
        Assert.True(service.SetPresetIcon(preset.Id, foreign.Id).IsFailed);
        Assert.Equal(own.Id, dbContext.Presets.AsNoTracking().Single(p => p.Id == preset.Id).IconAppId);
    }

    [Fact]
    public void RemoveAppId_ResetsThePresetIconItWasUsedFor()
    {
        var preset = service.AddPreset("Test");
        var appId = service.AddAppIds(preset.Id, ["620", "400"])[0];
        service.SetPresetIcon(preset.Id, appId.Id);

        service.RemoveAppId(preset.Id, appId.Id);

        Assert.Null(dbContext.Presets.AsNoTracking().Single().IconAppId);
    }
}
