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
            presetsService.AddAppIds(SelectedPreset.Id, appIds);
            foreach (string appId in appIds)
            {
                SelectedPreset.AppIds.Add(new(Guid.NewGuid(), appId));
            }
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
            }
        }
    }

    private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        if (SelectedPreset == null) return;
        greenLumaService.LoadAppList(SelectedPreset.Id);
        Mouse.OverrideCursor = null;
    }

    private void LoadAndLaunchSteamButton_Click(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        if (SelectedPreset == null) return;
        greenLumaService.LoadAppList(SelectedPreset.Id);
        greenLumaService.RestartSteam();
        Mouse.OverrideCursor = null;
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
        if (sender is TextBox textBox && textBox.DataContext is IEditableView editable)
        {
            if (editable is PresetView presetView)
            {
                presetsService.UpdatePreset(presetView.Id, presetView.Name);
            }
            else if (editable is AppIdView appIdView && selectedPreset != null)
            {
                presetsService.UpdateAppId(selectedPreset.Id, appIdView.Id, appIdView.AppId);
            }
            editable.IsEditing = false;
        }
    }

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox && textBox.DataContext is IEditableView editable)
        {
            if (editable is PresetView presetView)
            {
                presetsService.UpdatePreset(presetView.Id, presetView.Name);
            }
            else if (editable is AppIdView appIdView && selectedPreset != null)
            {
                presetsService.UpdateAppId(selectedPreset.Id, appIdView.Id, appIdView.AppId);
            }
            editable.IsEditing = false;
        }
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
        greenLumaService.ClearAppList();
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

        var shouldAddExclusion = response == MessageBoxResult.Yes;

        Mouse.OverrideCursor = Cursors.Wait;

        var localArchivePath = App.Current.Settings.GreenLumaArchivePath ?? string.Empty;
        var filesUrlDownload = App.Current.Settings.GreenLumaArchiveUrl ?? string.Empty;

        var result = useLocalGreenLumaArchive ? 
            greenLumaService.InstallGreenLumaOffline(localArchivePath, shouldAddExclusion) : 
            await greenLumaService.InstallGreenLuma(filesUrlDownload, shouldAddExclusion);

        if (result.IsSuccess)
        {
            dialogService.ShowInformation("Success", "GreenLuma has been successfully installed.");
        }
        else
        {
            dialogService.ShowWarning("Failed", result.Errors.Single().Message);
        }

        if (result.IsSuccess)
        {
            IsGreenLumaInstalled = true;
        }

        Mouse.OverrideCursor = null;
    }

    public void UninstallGreenLumaButton_Click(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;

        var result = greenLumaService.UninstallGreenLuma();
        if (result.IsSuccess)
        {
            dialogService.ShowInformation("Success", "GreenLuma has been successfully uninstalled.");
        }
        else
        {
            dialogService.ShowWarning("Failed", result.Errors.Single().Message);
        }

        if (result.IsSuccess)
        {
            IsGreenLumaInstalled = false;
        }

        Mouse.OverrideCursor = null;
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