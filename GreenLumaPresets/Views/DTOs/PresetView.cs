using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GreenLumaPresets.Models;

public class PresetView : INotifyPropertyChanged, IEditableView
{
    private bool isEditing;
    private string name;

    public Guid Id { get; private set; }
    public ObservableCollection<AppIdView> AppIds { get; private set; }

    public PresetView(Guid id, string name, IEnumerable<AppIdView> appIds)
    {
        Id = id;
        this.name = name;
        AppIds = new ObservableCollection<AppIdView>(appIds);
        isEditing = false;
    }

    public bool IsEditing 
    { 
        get => isEditing;
        set
        {
            isEditing = value;
            OnPropertyChanged();
        }
    }

    public string Name
    {
        get => name;
        set
        {
            name = value;
            OnPropertyChanged();
        }
    }

    public static PresetView From(Preset preset, IEnumerable<AppId> appIds)
        => new(preset.Id, preset.Name, appIds.Select(AppIdView.From));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}