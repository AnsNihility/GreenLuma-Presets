using FluentResults;
using GreenLumaPresets.Models;
using GreenLumaPresets.Services;
using GreenLumaPresets.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GreenLumaPresets.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly AppDbContext dbContext;
    private readonly PresetsService presetsService;
    private readonly FakeDialogService dialogs = new();
    private readonly FakeGreenLumaService greenLuma = new();
    private readonly FakeImportFromSteamDialog importDialog = new();
    private readonly FakeClipboardService clipboard = new();
    private readonly FakeUpdateService updates = new();

    public MainViewModelTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        dbContext.Database.Migrate();
        presetsService = new PresetsService(dbContext);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
    }

    private MainViewModel CreateViewModel(Settings? settings = null) => new(
        presetsService, greenLuma, updates, dialogs, importDialog, clipboard, settings ?? new Settings());

    private MainViewModel CreateWithPreset(params string[] appIds)
    {
        var preset = presetsService.AddPreset("Preset");
        presetsService.AddAppIds(preset.Id, appIds);
        var viewModel = CreateViewModel();
        viewModel.SelectedPreset = viewModel.Presets.Single();
        return viewModel;
    }

    [Fact]
    public void Constructor_LoadsPresetsWithTheirAppIdsInOrder()
    {
        var preset = presetsService.AddPreset("Portal");
        presetsService.AddAppIds(preset.Id, ["400", "323170"]);

        var viewModel = CreateViewModel();

        var loaded = Assert.Single(viewModel.Presets);
        Assert.Equal("Portal", loaded.Name);
        Assert.Equal(["400", "323170"], loaded.AppIds.Select(a => a.Value));
    }

    [Fact]
    public void PresetCommands_AreDisabledUntilAPresetIsSelected()
    {
        presetsService.AddPreset("Preset");
        var viewModel = CreateViewModel();

        Assert.False(viewModel.LoadPresetCommand.CanExecute(null));
        Assert.False(viewModel.CreateAppIdCommand.CanExecute(null));

        viewModel.SelectedPreset = viewModel.Presets[0];

        Assert.True(viewModel.HasSelectedPreset);
        Assert.True(viewModel.LoadPresetCommand.CanExecute(null));
        Assert.True(viewModel.LoadAndLaunchSteamCommand.CanExecute(null));
        Assert.True(viewModel.CreateAppIdCommand.CanExecute(null));
    }

    [Fact]
    public void CreatePreset_PersistsAndSelectsIt()
    {
        var viewModel = CreateViewModel();

        viewModel.CreatePresetCommand.Execute(null);

        Assert.Same(viewModel.SelectedPreset, Assert.Single(viewModel.Presets));
        Assert.Equal("New Preset", presetsService.GetPresets().Single().Name);
    }

    [Fact]
    public void ImportPreset_CreatesAPresetNamedAfterTheApp()
    {
        importDialog.Next = new SteamApp("Portal", ["400", "323170"]);
        var viewModel = CreateViewModel();

        viewModel.ImportPresetCommand.Execute(null);

        Assert.Equal("Portal", viewModel.SelectedPreset?.Name);
        Assert.Equal(["400", "323170"], viewModel.SelectedPreset!.AppIds.Select(a => a.Value));
    }

    [Fact]
    public void ImportPreset_Cancelled_DoesNothing()
    {
        var viewModel = CreateViewModel();

        viewModel.ImportPresetCommand.Execute(null);

        Assert.Empty(viewModel.Presets);
        Assert.Empty(presetsService.GetPresets());
    }

    [Fact]
    public void DeletePreset_RemovesItEverywhere()
    {
        var viewModel = CreateWithPreset("620");

        viewModel.DeletePresetCommand.Execute(viewModel.Presets[0]);

        Assert.Empty(viewModel.Presets);
        Assert.Empty(presetsService.GetPresets());
        Assert.Empty(dbContext.AppIds);
    }

    [Fact]
    public void CommitEdit_ValidAppId_IsNormalizedAndSaved()
    {
        var viewModel = CreateWithPreset("620");
        var appId = viewModel.SelectedPreset!.AppIds[0];

        viewModel.BeginEditCommand.Execute(appId);
        appId.Value = " 0730 ";
        viewModel.CommitEditCommand.Execute(appId);

        Assert.False(appId.IsEditing);
        Assert.Equal("730", appId.Value);
        Assert.Equal("730", presetsService.GetAppIdValue(appId.Id));
        Assert.Empty(dialogs.Shown);
    }

    [Fact]
    public void CommitEdit_InvalidAppId_IsReportedAndReverted()
    {
        var viewModel = CreateWithPreset("620");
        var appId = viewModel.SelectedPreset!.AppIds[0];

        viewModel.BeginEditCommand.Execute(appId);
        appId.Value = "abc";
        viewModel.CommitEditCommand.Execute(appId);

        Assert.Equal("620", appId.Value);
        Assert.Equal("Warning", Assert.Single(dialogs.Shown).Kind);
    }

    [Fact]
    public void CommitEdit_RunsOnlyOncePerEdit()
    {
        // Enter commits, then the TextBox losing focus commits again.
        var viewModel = CreateWithPreset("620");
        var appId = viewModel.SelectedPreset!.AppIds[0];

        viewModel.BeginEditCommand.Execute(appId);
        appId.Value = "abc";
        viewModel.CommitEditCommand.Execute(appId);
        viewModel.CommitEditCommand.Execute(appId);

        Assert.Single(dialogs.Shown);
    }

    [Fact]
    public void CommitEdit_EmptyPresetName_IsReverted()
    {
        var viewModel = CreateWithPreset();
        var preset = viewModel.SelectedPreset!;

        viewModel.BeginEditCommand.Execute(preset);
        preset.Name = "  ";
        viewModel.CommitEditCommand.Execute(preset);

        Assert.Equal("Preset", preset.Name);
    }

    [Fact]
    public void CancelEdit_RestoresTheStoredValue()
    {
        var viewModel = CreateWithPreset();
        var preset = viewModel.SelectedPreset!;

        viewModel.BeginEditCommand.Execute(preset);
        preset.Name = "Renamed";
        viewModel.CancelEditCommand.Execute(preset);

        Assert.False(preset.IsEditing);
        Assert.Equal("Preset", preset.Name);
        Assert.Equal("Preset", presetsService.GetPresetName(preset.Id));
    }

    [Fact]
    public void DeleteAppId_SelectsTheNextOne()
    {
        var viewModel = CreateWithPreset("1", "2", "3");
        var appIds = viewModel.SelectedPreset!.AppIds;
        viewModel.SelectedAppId = appIds[1];

        viewModel.DeleteAppIdCommand.Execute(null);

        Assert.Equal(["1", "3"], appIds.Select(a => a.Value));
        Assert.Equal("3", viewModel.SelectedAppId?.Value);
        Assert.Equal(["1", "3"], presetsService.GetAppIdsForPreset(viewModel.SelectedPreset.Id).Select(a => a.Value));
    }

    [Fact]
    public void DeleteAppId_LastOne_SelectsThePrevious()
    {
        var viewModel = CreateWithPreset("1", "2");
        viewModel.SelectedAppId = viewModel.SelectedPreset!.AppIds[1];

        viewModel.DeleteAppIdCommand.Execute(null);

        Assert.Equal("1", viewModel.SelectedAppId?.Value);
    }

    [Fact]
    public void ImportAppIdsFromClipboard_AddsValidValuesAndReportsSkippedOnes()
    {
        var viewModel = CreateWithPreset("620");
        clipboard.Text = "400\r\n620\r\nabc\n730";

        viewModel.ImportAppIdsFromClipboardCommand.Execute(null);

        Assert.Equal(["620", "400", "730"], viewModel.SelectedPreset!.AppIds.Select(a => a.Value));
        var summary = Assert.Single(dialogs.Shown);
        Assert.Contains("2 AppID(s) added", summary.Message);
        Assert.Contains("2 skipped", summary.Message);
    }

    [Fact]
    public void ImportAppIdsFromSteam_AddsTheAppAndItsDlc()
    {
        var viewModel = CreateWithPreset("620");
        importDialog.Next = new SteamApp("Portal", ["400", "323170"]);

        viewModel.ImportAppIdsFromSteamCommand.Execute(null);

        Assert.Equal(["620", "400", "323170"], viewModel.SelectedPreset!.AppIds.Select(a => a.Value));
        Assert.Empty(dialogs.Shown);
    }

    [Fact]
    public void LoadPreset_AboveTheConfiguredLimit_AsksAndCanBeDeclined()
    {
        var preset = presetsService.AddPreset("Big");
        presetsService.AddAppIds(preset.Id, ["1", "2", "3"]);
        var viewModel = CreateViewModel(new Settings { MaxAppListSize = 2 });
        viewModel.SelectedPreset = viewModel.Presets[0];
        dialogs.Answers.Enqueue(false);

        viewModel.LoadPresetCommand.Execute(null);

        Assert.Equal("Question", Assert.Single(dialogs.Shown).Kind);
        Assert.Empty(greenLuma.Calls);
    }

    [Fact]
    public void LoadAndLaunchSteam_DoesNotRestartSteamWhenLoadingFails()
    {
        var viewModel = CreateWithPreset("620");
        greenLuma.LoadAppListResult = Result.Fail("Access denied");

        viewModel.LoadAndLaunchSteamCommand.Execute(null);

        Assert.Equal(["LoadAppList"], greenLuma.Calls);
        var warning = Assert.Single(dialogs.Shown);
        Assert.Equal(("Warning", "Access denied"), (warning.Kind, warning.Message));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void LoadAndLaunchSteam_LoadsThenRestartsSteam()
    {
        var viewModel = CreateWithPreset("620");

        viewModel.LoadAndLaunchSteamCommand.Execute(null);

        Assert.Equal(["LoadAppList", "RestartSteam"], greenLuma.Calls);
        Assert.Empty(dialogs.Shown);
    }

    [Fact]
    public async Task Install_StopsWhenTheExclusionFailsAndTheUserDeclines()
    {
        var viewModel = CreateViewModel(new Settings { GreenLumaArchiveUrl = "https://example.invalid/gl.rar", GreenLumaArchivePath = "" });
        greenLuma.ExclusionResult = Result.Fail("Declined");
        dialogs.Answers.Enqueue(true);  // add the exclusion
        dialogs.Answers.Enqueue(false); // don't continue without it

        await viewModel.InstallOrUninstallGreenLumaCommand.ExecuteAsync(null);

        Assert.Equal(["AddDefenderExclusionAsync"], greenLuma.Calls);
        Assert.False(viewModel.IsGreenLumaInstalled);
    }

    [Fact]
    public async Task Install_FromUrl_UpdatesTheInstalledState()
    {
        var viewModel = CreateViewModel(new Settings { GreenLumaArchiveUrl = "https://example.invalid/gl.rar", GreenLumaArchivePath = "" });
        dialogs.Answers.Enqueue(false); // no exclusion

        await viewModel.InstallOrUninstallGreenLumaCommand.ExecuteAsync(null);

        Assert.Equal(["InstallGreenLumaFromUrlAsync"], greenLuma.Calls);
        Assert.True(viewModel.IsGreenLumaInstalled);
        Assert.Equal("_Uninstall GreenLuma", viewModel.InstallUninstallMenuText);
    }

    [Fact]
    public async Task Install_WithoutAnyArchiveConfigured_ShowsAnError()
    {
        var viewModel = CreateViewModel(new Settings { GreenLumaArchiveUrl = "", GreenLumaArchivePath = "" });

        await viewModel.InstallOrUninstallGreenLumaCommand.ExecuteAsync(null);

        Assert.Empty(greenLuma.Calls);
        Assert.Equal("Error", Assert.Single(dialogs.Shown).Kind);
    }

    [Fact]
    public async Task Uninstall_WhenInstalled_RefreshesTheState()
    {
        greenLuma.Installed = true;
        var viewModel = CreateViewModel();
        Assert.Equal("_Uninstall GreenLuma", viewModel.InstallUninstallMenuText);

        await viewModel.InstallOrUninstallGreenLumaCommand.ExecuteAsync(null);

        Assert.Equal(["UninstallGreenLuma"], greenLuma.Calls);
        Assert.False(viewModel.IsGreenLumaInstalled);
    }

    [Fact]
    public void DeleteSteamCache_FollowsTheExecutableAvailability()
    {
        Assert.False(CreateViewModel().DeleteSteamCacheCommand.CanExecute(null));

        greenLuma.DeleteCacheExeInstalled = true;
        Assert.True(CreateViewModel().DeleteSteamCacheCommand.CanExecute(null));
    }

    [Fact]
    public void ShowAbout_DisplaysTheVersion()
    {
        CreateViewModel().ShowAboutCommand.Execute(null);

        Assert.Contains("1.2.3.4", Assert.Single(dialogs.Shown).Message);
    }
}
