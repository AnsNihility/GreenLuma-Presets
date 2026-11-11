using CommunityToolkit.Mvvm.ComponentModel;
using GreenLumaPresets.Models;

namespace GreenLumaPresets.ViewModels;

public partial class AppIdItemViewModel : ObservableObject, IEditable
{
    public AppIdItemViewModel(Guid id, string value)
    {
        Id = id;
        Value = value;
    }

    public Guid Id { get; }

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    public static AppIdItemViewModel From(AppId appId) => new(appId.Id, appId.Value);
}
