using GreenLumaPresets.Controllers;
using GreenLumaPresets.Models;
using GreenLumaPresets.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GreenLumaPresets;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly PresetsService presetsService;
    private readonly GreenLumaService greenLumaService;
    private PresetView? selectedPreset;

    public ObservableCollection<PresetView> Presets { get; set; }
    public bool IsGreenLumaInstalled { get; init; }

    public string GreenLumaStatus => IsGreenLumaInstalled ? "installed" : "not installed";
    public string GreenLumaStatusColor => IsGreenLumaInstalled ? "Green" : "Red";

    public MainWindow()
    {
        InitializeComponent();

        presetsService = App.Current.Services.GetService<PresetsService>()
            ?? throw new ArgumentException(nameof(presetsService));

        greenLumaService = App.Current.Services.GetService<GreenLumaService>() 
            ?? throw new ArgumentException(nameof(greenLumaService));

        Presets = new(presetsService.GetPresetsWithAppIds().Select(x => PresetView.From(x.Key, x.Value)));
        IsGreenLumaInstalled = greenLumaService.IsGreenLumaInstalled();

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

    private async void LoadPresetButton_Click(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        await greenLumaService.LoadAppList();
        Mouse.OverrideCursor = null;
    }

    private async void LoadAndLaunchSteamButton_Click(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        await greenLumaService.LoadAppList();
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

    public PresetView? SelectedPreset
    {
        get => selectedPreset;
        set
        {
            selectedPreset = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}