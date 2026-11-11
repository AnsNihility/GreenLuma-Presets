using FluentResults;
using GreenLumaPresets.Controllers;
using GreenLumaPresets.Models;
using GreenLumaPresets.Services;
using GreenLumaPresets.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GreenLumaPresets;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly PresetsService presetsService;
    private readonly GreenLumaService greenLumaService;
    private readonly UpdateService updateService;
    private readonly IDialogService dialogService;
    private PresetView? selectedPreset;
    private bool isGreenLumaInstalled;

    public ObservableCollection<PresetView> Presets { get; set; }
    public bool IsDeleteCacheExeInstalled { get; set; }
    public bool CanInstallGreenLuma { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        presetsService = App.Current.Services.GetService<PresetsService>()
            ?? throw new ArgumentException(nameof(presetsService));

        greenLumaService = App.Current.Services.GetService<GreenLumaService>() 
            ?? throw new ArgumentException(nameof(greenLumaService));

        updateService = App.Current.Services.GetService<UpdateService>()
            ?? throw new ArgumentException(nameof(updateService));

        dialogService = App.Current.Services.GetService<IDialogService>()
            ?? throw new ArgumentException(nameof(dialogService));

        Presets = new(presetsService.GetPresetsWithAppIds().Select(x => PresetView.From(x.Key, x.Value)));

        IsGreenLumaInstalled = greenLumaService.IsGreenLumaInstalled();
        IsDeleteCacheExeInstalled = greenLumaService.IsDeleteCacheExeInstalled();
        CanInstallGreenLuma = App.Current.Settings.CanInstallGreenLuma;

        DataContext = this;
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        ImportContextMenu.PlacementTarget = sender as Button;
        ImportContextMenu.IsOpen = true;
    }

    private void ImportFromClipboardMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset != null)
        {
            string clipboardText = Clipboard.GetText();
            string[] appIds = clipboardText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var newAppIds = presetsService.AddAppIds(SelectedPreset.Id, appIds);
            foreach (AppId appId in newAppIds)
            {
                SelectedPreset.AppIds.Add(AppIdView.From(appId));
            }
            ShowImportSummary(appIds.Length, newAppIds.Count);
        }
    }

    private void ImportFromSteamDBMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset != null)
        {
            ImportFromSteamWindow window = new(this);
            window.ShowDialog();
            if (window.AppIds.Count > 0)
            {
                var newAppIds = presetsService.AddAppIds(SelectedPreset.Id, window.AppIds);
                foreach (AppId appId in newAppIds)
                {
                    SelectedPreset.AppIds.Add(AppIdView.From(appId));
                }
                ShowImportSummary(window.AppIds.Count, newAppIds.Count);
            }
        }
    }

    private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset == null || !ConfirmAppListSize(SelectedPreset)) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            ShowIfFailed(greenLumaService.LoadAppList(SelectedPreset.Id));
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void LoadAndLaunchSteamButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset == null || !ConfirmAppListSize(SelectedPreset)) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            if (!ShowIfFailed(greenLumaService.LoadAppList(SelectedPreset.Id))) return;
            ShowIfFailed(greenLumaService.RestartSteam());
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    /// <summary>
    /// Asks for confirmation when the preset exceeds the AppList size configured in appsettings.json.
    /// </summary>
    private bool ConfirmAppListSize(PresetView preset)
    {
        var maxSize = App.Current.Settings.MaxAppListSize;
        if (maxSize is not > 0 || preset.AppIds.Count <= maxSize) return true;

        var response = dialogService.ShowQuestion(
            "Preset too large",
            $"This preset contains {preset.AppIds.Count} AppIDs but GreenLuma is configured to support {maxSize}. "
            + "AppIDs beyond the limit may be ignored by GreenLuma.\n\nLoad it anyway?");
        return response == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Shows a warning dialog when the result failed.
    /// </summary>
    /// <returns>true when the result succeeded.</returns>
    private bool ShowIfFailed(Result result)
    {
        if (result.IsSuccess) return true;
        dialogService.ShowWarning("Failed", string.Join("\n", result.Errors.Select(x => x.Message)));
        return false;
    }

    private void DeleteAppIdMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is AppIdView appId && SelectedPreset != null)
        {
            SelectedPreset.AppIds.Remove(appId);
            presetsService.RemoveAppId(SelectedPreset.Id, appId.Id);
        }
    }

    private void RenameAppIdMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is AppIdView appId)
        {
            if (menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is TextBox textBox)
            {
                appId.IsEditing = true;
                textBox.Focus();
            }
        }
    }

    private void CreateAppIdButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset == null) return;
        var appId = presetsService.AddAppId(SelectedPreset.Id, "0");
        if (appId != null)
        {
            SelectedPreset.AppIds.Add(AppIdView.From(appId));
        }
    }

    private void CreatePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var preset = PresetView.From(presetsService.AddPreset("New Preset"), []);
        Presets.Add(preset);
        PresetsListBox.SelectedItem = preset;
    }

    private void RenamePresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is PresetView preset)
        {
            if (menuItem.Parent is ContextMenu contextMenu && 
                contextMenu.PlacementTarget is TextBox textBox)
            {
                preset.IsEditing = true;
                textBox.Focus();
                textBox.SelectAll();
            }
        }
    }

    private void DeletePresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is PresetView preset)
        {
            presetsService.RemovePreset(preset.Id);
            Presets.Remove(preset);
        }
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox) CommitEdit(textBox);
    }

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        if (e.Key == Key.Enter)
        {
            CommitEdit(textBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && textBox.DataContext is IEditableView { IsEditing: true } editable)
        {
            // Discard the typed text: the binding only pushes on LostFocus, so restore from the source.
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            editable.IsEditing = false;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Persists an inline edit once. Invalid values are reported and reverted to the stored value.
    /// </summary>
    private void CommitEdit(TextBox textBox)
    {
        if (textBox.DataContext is not IEditableView { IsEditing: true } editable) return;

        // Leaving edit mode first prevents the LostFocus raised by it from committing a second time.
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        editable.IsEditing = false;

        if (editable is PresetView presetView)
        {
            var result = presetsService.UpdatePreset(presetView.Id, presetView.Name);
            if (ShowIfFailed(result))
            {
                presetView.Name = presetView.Name.Trim();
            }
            else
            {
                presetView.Name = presetsService.GetPresetName(presetView.Id) ?? presetView.Name;
            }
        }
        else if (editable is AppIdView appIdView && SelectedPreset != null)
        {
            var result = presetsService.UpdateAppId(SelectedPreset.Id, appIdView.Id, appIdView.AppId);
            appIdView.AppId = ShowIfFailed(result.ToResult())
                ? result.Value
                : presetsService.GetAppIdValue(appIdView.Id) ?? appIdView.AppId;
        }
    }

    private void ShowImportSummary(int requested, int added)
    {
        var skipped = requested - added;
        if (skipped <= 0) return;

        dialogService.ShowInformation(
            "Import",
            $"{added} AppID(s) added.\n{skipped} skipped because they were invalid or already in the preset.");
    }

    private void AppIdsListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && sender is ListBox listBox && SelectedPreset != null)
        {
            var selectedItem = listBox.SelectedIndex;
            if (selectedItem < 0) return;
            presetsService.RemoveAppId(SelectedPreset.Id, SelectedPreset.AppIds[selectedItem].Id);
            SelectedPreset.AppIds.RemoveAt(selectedItem);
            listBox.SelectedIndex = selectedItem;
        }
    }

    private void ImportPresetButton_Click(object sender, RoutedEventArgs e)
    {
        ImportFromSteamWindow window = new(this);
        window.ShowDialog();
        if (window.AppIds.Count > 0 && !string.IsNullOrEmpty(window.AppName))
        {
            var newPreset = presetsService.AddPreset(window.AppName);
            var newAppIds = presetsService.AddAppIds(newPreset.Id, window.AppIds);

            var newPresetView = PresetView.From(newPreset, []);
            Presets.Add(newPresetView);
            SelectedPreset = newPresetView;
            foreach (AppId appId in newAppIds)
            {
                SelectedPreset.AppIds.Add(AppIdView.From(appId));
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (ResizeMode != ResizeMode.NoResize)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void TitleBar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        SystemCommands.ShowSystemMenu(this, screenPoint);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ResetPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ShowIfFailed(greenLumaService.ClearAppList())) return;
        dialogService.ShowInformation(
            "Clear IDs",
            "Loaded App IDs were cleared from Steam.");
    }

    private void DeleteCacheButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsDeleteCacheExeInstalled) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            greenLumaService.DeleteSteamAppCache();
        }
        catch (Exception ex)
        {
            dialogService.ShowError(
                "Error",
                $"Failed to clear Steam App Cache: {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private async void InstallGreenLumaButton_Click(object sender, RoutedEventArgs e)
    {
        var canInstallGreenLuma = App.Current.Settings.CanInstallGreenLuma;
        var useLocalGreenLumaArchive = App.Current.Settings.UseLocalGreenLumaArchive;

        if (!canInstallGreenLuma && !useLocalGreenLumaArchive)
        {
            dialogService.ShowError(
                "Error",
                "GreenLuma download URL and archive path are not set or are invalid in the configuration file \"appsettings.json\". If the archive path is set and valid make sure the file on the path provided exists.");
            return;
        }

        var response = dialogService.ShowQuestion(
            "Add Exclusion?",
            "Do you want to add an exclusion to Windows Defender for the Steam folder? This is recommended to prevent issues with GreenLuma.");

        if (response == MessageBoxResult.Yes)
        {
            Result exclusionResult;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                exclusionResult = await greenLumaService.AddDefenderExclusionAsync();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            if (exclusionResult.IsFailed)
            {
                var continueResponse = dialogService.ShowQuestion(
                    "Exclusion not added",
                    $"{exclusionResult.Errors.First().Message}\n\nContinue the installation without the exclusion?");
                if (continueResponse != MessageBoxResult.Yes) return;
            }
        }

        var localArchivePath = App.Current.Settings.ResolvedGreenLumaArchivePath;
        var filesUrlDownload = App.Current.Settings.GreenLumaArchiveUrl ?? string.Empty;

        Result result;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            result = useLocalGreenLumaArchive
                ? await greenLumaService.InstallGreenLumaFromArchiveAsync(localArchivePath)
                : await greenLumaService.InstallGreenLumaFromUrlAsync(filesUrlDownload);
            IsGreenLumaInstalled = greenLumaService.IsGreenLumaInstalled();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        if (ShowIfFailed(result))
        {
            dialogService.ShowInformation("Success", "GreenLuma has been successfully installed.");
        }
    }

    public void UninstallGreenLumaButton_Click(object sender, RoutedEventArgs e)
    {
        Result result;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            result = greenLumaService.UninstallGreenLuma();
            // Re-check the files: a partial uninstall may have removed only some of them.
            IsGreenLumaInstalled = greenLumaService.IsGreenLumaInstalled();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        if (ShowIfFailed(result))
        {
            dialogService.ShowInformation("Success", "GreenLuma has been successfully uninstalled.");
        }
    }

    private void InstallUninstallGreenLumaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (IsGreenLumaInstalled)
        {
            UninstallGreenLumaButton_Click(sender, e);
        }
        else
        {
            InstallGreenLumaButton_Click(sender, e);
        }
    }

    public Visibility InstallButtonVisibility { get => IsGreenLumaInstalled ? Visibility.Collapsed : Visibility.Visible; }
    public Visibility UninstallButtonVisibility { get => !IsGreenLumaInstalled ? Visibility.Collapsed : Visibility.Visible; }
    public string InstallUninstallMenuText { get => IsGreenLumaInstalled ? "_Uninstall GreenLuma" : "_Install GreenLuma"; }

    public PresetView? SelectedPreset
    {
        get => selectedPreset;
        set
        {
            selectedPreset = value;
            OnPropertyChanged();
        }
    }

    public bool IsGreenLumaInstalled
    {
        get => isGreenLumaInstalled;
        set
        {
            isGreenLumaInstalled = value;
            IsDeleteCacheExeInstalled = greenLumaService.IsDeleteCacheExeInstalled();
            OnPropertyChanged();
            OnPropertyChanged(nameof(InstallButtonVisibility));
            OnPropertyChanged(nameof(UninstallButtonVisibility));
            OnPropertyChanged(nameof(InstallUninstallMenuText));
            OnPropertyChanged(nameof(IsDeleteCacheExeInstalled));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void CheckForUpdatesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        updateService.CheckForUpdates(showNoUpdateMessage: true);
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var version = updateService.GetCurrentVersion();
        dialogService.ShowInformation(
            "About GreenLuma Presets",
            $"GreenLuma Presets Manager\n\nVersion: {version}\n\nA tool to manage GreenLuma presets and AppIDs.");
    }

    private void TutorialMenuItem_Click(object sender, RoutedEventArgs e)
    {
        const string tutorialMessage = @"Before starting let me explain some basics:
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

        dialogService.ShowInformation("Tutorial", tutorialMessage);
    }
}