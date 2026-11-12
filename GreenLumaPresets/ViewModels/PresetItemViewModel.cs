using CommunityToolkit.Mvvm.ComponentModel;
using GreenLumaPresets.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace GreenLumaPresets.ViewModels;

public partial class PresetItemViewModel : ObservableObject, IEditable
{
    public PresetItemViewModel(Guid id, string name, IEnumerable<AppIdItemViewModel> appIds, Guid? iconAppId = null)
    {
        Id = id;
        Name = name;
        this.iconAppId = iconAppId;
        AppIds = new(appIds);

        foreach (var appId in AppIds) Watch(appId);
        AppIds.CollectionChanged += OnAppIdsChanged;
    }

    public Guid Id { get; }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    /// <summary>
    /// AppID whose icon was picked; null for the automatic icon.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconPath), nameof(IconChoices))]
    private Guid? iconAppId;

    public ObservableCollection<AppIdItemViewModel> AppIds { get; }

    /// <summary>
    /// The picked AppID's icon, otherwise the first one in the list.
    /// A picked AppID without an icon (yet) falls back to the automatic icon.
    /// </summary>
    public string? IconPath
        => AppIds.FirstOrDefault(a => a.Id == IconAppId && a.IconPath != null)?.IconPath
        ?? AppIds.FirstOrDefault(a => a.IconPath != null)?.IconPath;

    /// <summary>
    /// "Automatic" followed by each distinct icon of the AppIDs: DLC often share their game's.
    /// </summary>
    public IReadOnlyList<PresetIconChoice> IconChoices
    {
        get
        {
            var picked = AppIds.FirstOrDefault(a => a.Id == IconAppId && a.IconPath != null);
            var choices = new List<PresetIconChoice> { new(this, null, "Automatic", null, picked == null) };
            choices.AddRange(AppIds
                .Where(a => a.IconPath != null)
                .GroupBy(a => a.IconPath)
                // Named after the picked AppID, else the app the icon belongs to rather than one of its DLC.
                .Select(g => g.FirstOrDefault(a => a == picked)
                    ?? g.FirstOrDefault(a => a.IconUrl?.Contains($"/apps/{a.Value}/") == true)
                    ?? g.First())
                .Select(a => new PresetIconChoice(
                    this,
                    a,
                    a.Name.Text.Length > 0 ? $"{a.Name.Text} ({a.Value})" : a.Value,
                    a.IconPath,
                    a == picked)));
            return choices;
        }
    }

    private void OnAppIdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (AppIdItemViewModel appId in e.OldItems ?? Array.Empty<AppIdItemViewModel>())
        {
            appId.PropertyChanged -= OnAppIdChanged;
            appId.Name.PropertyChanged -= OnAppIdNameChanged;
        }
        foreach (AppIdItemViewModel appId in e.NewItems ?? Array.Empty<AppIdItemViewModel>())
            Watch(appId);

        OnIconsChanged();
    }

    private void Watch(AppIdItemViewModel appId)
    {
        appId.PropertyChanged += OnAppIdChanged;
        appId.Name.PropertyChanged += OnAppIdNameChanged;
    }

    private void OnAppIdChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppIdItemViewModel.IconPath)) OnIconsChanged();
        else if (e.PropertyName is nameof(AppIdItemViewModel.Value)) OnPropertyChanged(nameof(IconChoices));
    }

    // The icon menu labels show the names.
    private void OnAppIdNameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppIdNameViewModel.Text)) OnPropertyChanged(nameof(IconChoices));
    }

    private void OnIconsChanged()
    {
        OnPropertyChanged(nameof(IconPath));
        OnPropertyChanged(nameof(IconChoices));
    }

    public static PresetItemViewModel From(Preset preset, IEnumerable<AppId> appIds)
        => new(preset.Id, preset.Name, appIds.Select(AppIdItemViewModel.From), preset.IconAppId);
}

/// <summary>
/// An entry of a preset's icon menu; <see cref="AppId"/> is null for the automatic icon.
/// </summary>
public record PresetIconChoice(PresetItemViewModel Preset, AppIdItemViewModel? AppId, string Label, string? IconPath, bool IsSelected);
