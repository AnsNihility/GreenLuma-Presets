using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GreenLumaPresets.Models;

public class AppIdView : INotifyPropertyChanged, IEditableView
{
    private string appId;
    private bool isEditing;

    public Guid Id { get; private set; }

    public AppIdView(Guid id, string appId)
    {
        Id = id;
        this.appId = appId;
        isEditing = false;
    }

    public string AppId
    {
        get => appId;
        set
        {
            appId = value;
            OnPropertyChanged();
        }
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

    public static AppIdView From(AppId appId)
        => new(appId.Id, appId.Value);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}