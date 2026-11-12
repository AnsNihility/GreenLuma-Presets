using CommunityToolkit.Mvvm.ComponentModel;
using GreenLumaPresets.Models;
using System.Collections.ObjectModel;

namespace GreenLumaPresets.ViewModels;

public partial class PresetItemViewModel : ObservableObject, IEditable
{
    public PresetItemViewModel(Guid id, string name, IEnumerable<AppIdItemViewModel> appIds)
    {
        Id = id;
        Name = name;
        AppIds = new(appIds);
    }

    public Guid Id { get; }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    public ObservableCollection<AppIdItemViewModel> AppIds { get; }

    public static PresetItemViewModel From(Preset preset, IEnumerable<AppId> appIds)
        => new(preset.Id, preset.Name, appIds.Select(AppIdItemViewModel.From));
}
