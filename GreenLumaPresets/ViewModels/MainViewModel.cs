using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentResults;
using GreenLumaPresets.Models;
using GreenLumaPresets.Services;
using System.Collections.ObjectModel;

namespace GreenLumaPresets.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPresetsService presetsService;
    private readonly IGreenLumaService greenLumaService;
    private readonly IUpdateService updateService;
    private readonly IDialogService dialogService;
    private readonly IImportFromSteamDialog importFromSteamDialog;
    private readonly IClipboardService clipboardService;
    private readonly Settings settings;

    public MainViewModel(
        IPresetsService presetsService,
        IGreenLumaService greenLumaService,
        IUpdateService updateService,
        IDialogService dialogService,
        IImportFromSteamDialog importFromSteamDialog,
        IClipboardService clipboardService,
        Settings settings)
    {
        this.presetsService = presetsService;
        this.greenLumaService = greenLumaService;
        this.updateService = updateService;
        this.dialogService = dialogService;
        this.importFromSteamDialog = importFromSteamDialog;
        this.clipboardService = clipboardService;
        this.settings = settings;

        Presets = new(presetsService.GetPresetsWithAppIds().Select(x => PresetItemViewModel.From(x.Key, x.Value)));
        RefreshGreenLumaState();
    }

    public ObservableCollection<PresetItemViewModel> Presets { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPreset))]
    [NotifyCanExecuteChangedFor(
        nameof(CreateAppIdCommand),
        nameof(ImportAppIdsFromSteamCommand),
        nameof(ImportAppIdsFromClipboardCommand),
        nameof(LoadPresetCommand),
        nameof(LoadAndLaunchSteamCommand))]
    private PresetItemViewModel? selectedPreset;

    [ObservableProperty]
    private AppIdItemViewModel? selectedAppId;

    public bool HasSelectedPreset => SelectedPreset != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallUninstallMenuText))]
    private bool isGreenLumaInstalled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSteamCacheCommand))]
    private bool isDeleteCacheExeInstalled;

    /// <summary>
    /// A long operation is running: the view shows a wait cursor.
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    public string InstallUninstallMenuText => IsGreenLumaInstalled ? "_Uninstall GreenLuma" : "_Install GreenLuma";

    #region Presets

    [RelayCommand]
    private void CreatePreset()
    {
        var preset = PresetItemViewModel.From(presetsService.AddPreset("New Preset"), []);
        Presets.Add(preset);
        SelectedPreset = preset;
    }

    [RelayCommand]
    private void ImportPreset()
    {
        var app = importFromSteamDialog.Show();
        if (app is not { AppIds.Count: > 0 } || string.IsNullOrEmpty(app.Name)) return;

        var newPreset = presetsService.AddPreset(app.Name);
        var newAppIds = presetsService.AddAppIds(newPreset.Id, app.AppIds);

        var preset = PresetItemViewModel.From(newPreset, newAppIds);
        Presets.Add(preset);
        SelectedPreset = preset;
    }

    [RelayCommand]
    private void DeletePreset(PresetItemViewModel? preset)
    {
        if (preset == null) return;

        presetsService.RemovePreset(preset.Id);
        Presets.Remove(preset);
    }

    #endregion

    #region Inline editing

    [RelayCommand]
    private void BeginEdit(IEditable? item)
    {
        if (item != null) item.IsEditing = true;
    }

    /// <summary>
    /// Persists an inline edit once. Invalid values are reported and reverted to the stored value.
    /// </summary>
    [RelayCommand]
    private void CommitEdit(IEditable? item)
    {
        if (item is not { IsEditing: true }) return;

        // Leaving edit mode first: the focus change it causes must not commit a second time.
        item.IsEditing = false;

        switch (item)
        {
            case PresetItemViewModel preset:
                var presetResult = presetsService.UpdatePreset(preset.Id, preset.Name);
                preset.Name = ShowIfFailed(presetResult)
                    ? preset.Name.Trim()
                    : presetsService.GetPresetName(preset.Id) ?? preset.Name;
                break;

            case AppIdItemViewModel appId when SelectedPreset != null:
                var appIdResult = presetsService.UpdateAppId(SelectedPreset.Id, appId.Id, appId.Value);
                appId.Value = ShowIfFailed(appIdResult.ToResult())
                    ? appIdResult.Value
                    : presetsService.GetAppIdValue(appId.Id) ?? appId.Value;
                break;
        }
    }

    [RelayCommand]
    private void CancelEdit(IEditable? item)
    {
        if (item is not { IsEditing: true }) return;

        item.IsEditing = false;
        switch (item)
        {
            case PresetItemViewModel preset:
                preset.Name = presetsService.GetPresetName(preset.Id) ?? preset.Name;
                break;
            case AppIdItemViewModel appId:
                appId.Value = presetsService.GetAppIdValue(appId.Id) ?? appId.Value;
                break;
        }
    }

    #endregion

    #region AppIDs

    [RelayCommand(CanExecute = nameof(HasSelectedPreset))]
    private void CreateAppId()
    {
        var appId = presetsService.AddAppId(SelectedPreset!.Id, AppIdRules.Placeholder);
        if (appId != null)
        {
            SelectedPreset.AppIds.Add(AppIdItemViewModel.From(appId));
        }
    }

    /// <summary>
    /// Deletes the given AppID, or the selected one, then selects the next AppID.
    /// </summary>
    [RelayCommand]
    private void DeleteAppId(AppIdItemViewModel? appId)
    {
        appId ??= SelectedAppId;
        if (appId == null || SelectedPreset == null) return;

        var appIds = SelectedPreset.AppIds;
        var index = appIds.IndexOf(appId);
        if (index < 0) return;

        presetsService.RemoveAppId(SelectedPreset.Id, appId.Id);
        appIds.RemoveAt(index);

        SelectedAppId = appIds.Count == 0 ? null : appIds[Math.Min(index, appIds.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPreset))]
    private void ImportAppIdsFromSteam()
    {
        var app = importFromSteamDialog.Show();
        if (app is not { AppIds.Count: > 0 }) return;

        AddAppIdsToSelectedPreset(app.AppIds);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPreset))]
    private void ImportAppIdsFromClipboard()
    {
        var appIds = clipboardService.GetText()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        AddAppIdsToSelectedPreset(appIds);
    }

    private void AddAppIdsToSelectedPreset(IReadOnlyCollection<string> appIds)
    {
        if (SelectedPreset == null) return;

        var newAppIds = presetsService.AddAppIds(SelectedPreset.Id, appIds);
        foreach (var appId in newAppIds)
        {
            SelectedPreset.AppIds.Add(AppIdItemViewModel.From(appId));
        }

        var skipped = appIds.Count - newAppIds.Count;
        if (skipped > 0)
        {
            dialogService.ShowInformation(
                "Import",
                $"{newAppIds.Count} AppID(s) added.\n{skipped} skipped because they were invalid or already in the preset.");
        }
    }

    #endregion

    #region GreenLuma

    [RelayCommand(CanExecute = nameof(HasSelectedPreset))]
    private void LoadPreset()
    {
        if (!ConfirmAppListSize(SelectedPreset!)) return;

        using (Busy())
        {
            ShowIfFailed(greenLumaService.LoadAppList(SelectedPreset!.Id));
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPreset))]
    private void LoadAndLaunchSteam()
    {
        if (!ConfirmAppListSize(SelectedPreset!)) return;

        using (Busy())
        {
            if (!ShowIfFailed(greenLumaService.LoadAppList(SelectedPreset!.Id))) return;
            ShowIfFailed(greenLumaService.RestartSteam());
        }
    }

    [RelayCommand]
    private void ClearAppList()
    {
        if (!ShowIfFailed(greenLumaService.ClearAppList())) return;
        dialogService.ShowInformation("Clear IDs", "Loaded App IDs were cleared from Steam.");
    }

    [RelayCommand(CanExecute = nameof(IsDeleteCacheExeInstalled))]
    private void DeleteSteamCache()
    {
        using (Busy())
        {
            try
            {
                greenLumaService.DeleteSteamAppCache();
            }
            catch (Exception ex)
            {
                dialogService.ShowError("Error", $"Failed to clear Steam App Cache: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private Task InstallOrUninstallGreenLumaAsync()
    {
        if (IsGreenLumaInstalled)
        {
            UninstallGreenLuma();
            return Task.CompletedTask;
        }
        return InstallGreenLumaAsync();
    }

    private async Task InstallGreenLumaAsync()
    {
        if (!settings.CanInstallGreenLuma && !settings.UseLocalGreenLumaArchive)
        {
            dialogService.ShowError(
                "Error",
                "No GreenLuma archive found. Place GreenLuma.rar next to GreenLuma Presets, or copy \"appsettings.example.json\" to \"appsettings.json\" and set GreenLumaArchivePath or GreenLumaArchiveUrl.");
            return;
        }

        var addExclusion = dialogService.Confirm(
            "Add Exclusion?",
            "Do you want to add an exclusion to Windows Defender for the Steam folder? This is recommended to prevent issues with GreenLuma.\n\nWindows will ask for administrator permission.");

        if (addExclusion)
        {
            Result exclusionResult;
            using (Busy())
            {
                exclusionResult = await greenLumaService.AddDefenderExclusionAsync();
            }

            if (exclusionResult.IsFailed)
            {
                var continueInstallation = dialogService.Confirm(
                    "Exclusion not added",
                    $"{exclusionResult.Errors.First().Message}\n\nContinue the installation without the exclusion?");
                if (!continueInstallation) return;
            }
        }

        Result result;
        using (Busy())
        {
            result = settings.UseLocalGreenLumaArchive
                ? await greenLumaService.InstallGreenLumaFromArchiveAsync(settings.ResolvedGreenLumaArchivePath, settings.GreenLumaArchiveSha256)
                : await greenLumaService.InstallGreenLumaFromUrlAsync(settings.GreenLumaArchiveUrl ?? string.Empty, settings.GreenLumaArchiveSha256);
            RefreshGreenLumaState();
        }

        if (ShowIfFailed(result))
        {
            dialogService.ShowInformation("Success", "GreenLuma has been successfully installed.");
        }
    }

    private void UninstallGreenLuma()
    {
        Result result;
        using (Busy())
        {
            result = greenLumaService.UninstallGreenLuma();
            // Re-check the files: a partial uninstall may have removed only some of them.
            RefreshGreenLumaState();
        }

        if (ShowIfFailed(result))
        {
            dialogService.ShowInformation("Success", "GreenLuma has been successfully uninstalled.");
        }
    }

    private void RefreshGreenLumaState()
    {
        IsGreenLumaInstalled = greenLumaService.IsGreenLumaInstalled();
        IsDeleteCacheExeInstalled = greenLumaService.IsDeleteCacheExeInstalled();
    }

    /// <summary>
    /// Asks for confirmation when the preset exceeds the AppList size configured in appsettings.json.
    /// </summary>
    private bool ConfirmAppListSize(PresetItemViewModel preset)
    {
        var maxSize = settings.MaxAppListSize;
        if (maxSize is not > 0 || preset.AppIds.Count <= maxSize) return true;

        return dialogService.Confirm(
            "Preset too large",
            $"This preset contains {preset.AppIds.Count} AppIDs but GreenLuma is configured to support {maxSize}. "
            + "AppIDs beyond the limit may be ignored by GreenLuma.\n\nLoad it anyway?");
    }

    #endregion

    #region Help

    [RelayCommand]
    private void CheckForUpdates() => updateService.CheckForUpdates(showNoUpdateMessage: true);

    [RelayCommand]
    private void ShowAbout()
    {
        dialogService.ShowInformation(
            "About GreenLuma Presets",
            $"GreenLuma Presets Manager\n\nVersion: {updateService.GetCurrentVersion()}\n\nA tool to manage GreenLuma presets and AppIDs.");
    }

    [RelayCommand]
    private void ShowTutorial() => dialogService.ShowInformation("Tutorial", TutorialMessage);

    private const string TutorialMessage = @"Before starting let me explain some basics:
- GreenLuma: a tool that allows you to unlock apps in Steam by loading the list of AppIDs you want to unlock
- AppID: a unique identifier for each game and DLCs in Steam
- Preset: a collection of AppIDs that can be loaded into GreenLuma

Quick start:
1) Create a preset: You have two options to create a preset
   - Create empty preset: click Add below the presets list to create a new preset with no AppIDs
   - Import preset from Steam: click Import and enter the AppID of a game to import all its DLCs as a preset. You can find the AppID on the game's SteamDB page. For example, the AppID for Portal 2 is 620, so entering 620 will import a preset with all Portal 2 DLCs.
2) Add AppIDs: You also have two options to add AppIDs to an existing preset
   - Add single AppID: click Add below the AppIDs list to add a new AppID with value 0 that you can edit. You can find the AppID of a game or DLC on its SteamDB page.
   - Import AppIDs from Steam: click Import and then click on Import from Steam. Enter the AppID of a game to import all its DLCs as AppIDs into the selected preset.
   - Import AppIDs from clipboard: copy a list of AppIDs to the clipboard and click Import, then click on Import from clipboard and it will add all AppIDs from the clipboard to the selected preset. This is useful if you want to import a list of AppIDs from a text file or a website. Just make sure to copy only the AppIDs, one per line, without any additional text.
3) Load and launch: use Load and launch Steam to restart Steam with the selected preset.

Tips:
- Right-click a preset or AppID to rename or delete.
- Clear IDs from GreenLuma removes the current AppList in Steam.
- Check for Updates is under Help.";

    #endregion

    private bool ShowIfFailed(Result result)
    {
        if (result.IsSuccess) return true;
        dialogService.ShowWarning("Failed", string.Join("\n", result.Errors.Select(x => x.Message)));
        return false;
    }

    private BusyScope Busy() => new(this);

    private readonly struct BusyScope : IDisposable
    {
        private readonly MainViewModel owner;

        public BusyScope(MainViewModel owner)
        {
            this.owner = owner;
            owner.IsBusy = true;
        }

        public void Dispose() => owner.IsBusy = false;
    }
}
