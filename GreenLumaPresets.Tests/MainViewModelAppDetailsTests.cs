using FluentResults;
using GreenLumaPresets.Models;
using GreenLumaPresets.Services;
using GreenLumaPresets.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GreenLumaPresets.Tests;

/// <summary>
/// AppID names and icons, as handled by <see cref="MainViewModel"/>.
/// </summary>
public sealed class MainViewModelAppDetailsTests : IDisposable
{
    private const string PortalIcon = "https://icons/620.jpg";

    private readonly SqliteConnection connection;
    private readonly AppDbContext dbContext;
    private readonly PresetsService presetsService;
    private readonly FakeDialogService dialogs = new();
    private readonly FakeImportFromSteamDialog importDialog = new();
    private readonly FakeClipboardService clipboard = new();
    private readonly FakeSteamService steam = new();
    private readonly FakeIconCache icons = new();

    public MainViewModelAppDetailsTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        dbContext.Database.Migrate();
        presetsService = new PresetsService(dbContext);

        steam.Details[620] = new SteamAppDetails("Portal 2", PortalIcon);
        steam.Details[400] = new SteamAppDetails("Portal", null);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
    }

    private async Task<MainViewModel> CreateViewModelAsync()
    {
        var viewModel = new MainViewModel(
            presetsService, new FakeGreenLumaService(), new FakeUpdateService(), dialogs, importDialog, clipboard, steam, icons, new Settings());
        await viewModel.Initialization;
        viewModel.SelectedPreset = viewModel.Presets.FirstOrDefault();
        return viewModel;
    }

    [Fact]
    public async Task Startup_LoadsStoredIconsAndOnlyFetchesMissingNames()
    {
        var preset = presetsService.AddPreset("Preset");
        presetsService.AddAppIds(preset.Id, ["620", "400"]);
        presetsService.ApplyAppDetails(new Dictionary<uint, SteamAppDetails> { [620] = new("Portal 2", PortalIcon) });

        var viewModel = await CreateViewModelAsync();

        Assert.Equal([400u], Assert.Single(steam.DetailRequests));
        var appIds = viewModel.SelectedPreset!.AppIds;
        Assert.Equal(["Portal 2", "Portal"], appIds.Select(a => a.Name.Text));
        Assert.Equal($"cache/{PortalIcon}", appIds[0].IconPath);
        Assert.Null(appIds[1].IconPath);
    }

    [Fact]
    public async Task Startup_SteamUnreachable_StaysSilent()
    {
        var preset = presetsService.AddPreset("Preset");
        presetsService.AddAppIds(preset.Id, ["620"]);
        steam.DetailsFailure = Result.Fail("Couldn't connect to Steam API");

        var viewModel = await CreateViewModelAsync();

        Assert.Empty(dialogs.Shown);
        Assert.Equal("", viewModel.SelectedPreset!.AppIds.Single().Name.Text);
    }

    [Fact]
    public async Task ImportFromClipboard_FetchesNamesAndIconsOfTheNewAppIds()
    {
        presetsService.AddPreset("Preset");
        var viewModel = await CreateViewModelAsync();
        clipboard.Text = "620\n400";

        await viewModel.ImportAppIdsFromClipboardCommand.ExecuteAsync(null);

        var appIds = viewModel.SelectedPreset!.AppIds;
        Assert.Equal(["Portal 2", "Portal"], appIds.Select(a => a.Name.Text));
        Assert.Equal($"cache/{PortalIcon}", appIds[0].IconPath);
        Assert.Equal("Portal 2", presetsService.GetAppIdName(appIds[0].Id));
    }

    [Fact]
    public async Task ImportPreset_NamesItsAppIds()
    {
        importDialog.Next = new SteamApp("Portal 2", ["620"]);
        var viewModel = await CreateViewModelAsync();

        await viewModel.ImportPresetCommand.ExecuteAsync(null);

        Assert.Equal("Portal 2", viewModel.SelectedPreset!.AppIds.Single().Name.Text);
    }

    [Fact]
    public async Task FetchedDetails_UpdateTheSameAppIdInOtherPresets()
    {
        var first = presetsService.AddPreset("First");
        presetsService.AddAppIds(first.Id, ["730"]);
        var second = presetsService.AddPreset("Second");
        presetsService.AddAppIds(second.Id, ["620"]);
        steam.DetailsFailure = Result.Fail("offline");
        var viewModel = await CreateViewModelAsync();
        steam.DetailsFailure = null;
        clipboard.Text = "620";

        await viewModel.ImportAppIdsFromClipboardCommand.ExecuteAsync(null);

        var other = viewModel.Presets.Single(p => p.Name == "Second").AppIds.Single();
        Assert.Equal("Portal 2", other.Name.Text);
        Assert.Equal($"cache/{PortalIcon}", other.IconPath);
    }

    [Fact]
    public async Task CommitEdit_NewAppIdValue_ReplacesTheNameAndIcon()
    {
        var preset = presetsService.AddPreset("Preset");
        presetsService.AddAppIds(preset.Id, ["620"]);
        var viewModel = await CreateViewModelAsync();
        var appId = viewModel.SelectedPreset!.AppIds.Single();

        viewModel.BeginEditCommand.Execute(appId);
        appId.Value = "400";
        await viewModel.CommitEditCommand.ExecuteAsync(appId);

        Assert.Equal("Portal", appId.Name.Text);
        Assert.Null(appId.IconUrl);
        Assert.Null(appId.IconPath);
    }

    [Fact]
    public async Task CommitEdit_Name_IsTrimmedSavedAndKeptByRefresh()
    {
        var preset = presetsService.AddPreset("Preset");
        presetsService.AddAppIds(preset.Id, ["620"]);
        var viewModel = await CreateViewModelAsync();
        var name = viewModel.SelectedPreset!.AppIds.Single().Name;

        viewModel.BeginEditCommand.Execute(name);
        name.Text = "  Portal 2 (co-op)  ";
        await viewModel.CommitEditCommand.ExecuteAsync(name);
        await viewModel.RefreshAppDetailsCommand.ExecuteAsync(viewModel.SelectedPreset);

        Assert.False(name.IsEditing);
        Assert.Equal("Portal 2 (co-op)", name.Text);
        Assert.Equal("Portal 2 (co-op)", presetsService.GetAppIdName(name.AppId));
    }

    [Fact]
    public async Task CancelEdit_Name_RestoresTheStoredName()
    {
        var preset = presetsService.AddPreset("Preset");
        presetsService.AddAppIds(preset.Id, ["620"]);
        var viewModel = await CreateViewModelAsync();
        var name = viewModel.SelectedPreset!.AppIds.Single().Name;

        viewModel.BeginEditCommand.Execute(name);
        name.Text = "Typo";
        viewModel.CancelEditCommand.Execute(name);

        Assert.Equal("Portal 2", name.Text);
    }

    [Fact]
    public async Task Refresh_SteamUnreachable_IsReported()
    {
        presetsService.AddAppIds(presetsService.AddPreset("Preset").Id, ["620"]);
        var viewModel = await CreateViewModelAsync();
        steam.DetailsFailure = Result.Fail("Couldn't connect to Steam API");

        await viewModel.RefreshAppDetailsCommand.ExecuteAsync(viewModel.SelectedPreset);

        var shown = Assert.Single(dialogs.Shown);
        Assert.Equal("Couldn't connect to Steam API", shown.Message);
        Assert.False(viewModel.IsBusy);
    }
}
