using CommunityToolkit.Mvvm.ComponentModel;
using GreenLumaPresets.Models;

namespace GreenLumaPresets.ViewModels;

public partial class AppIdItemViewModel : ObservableObject, IEditable
{
    public AppIdItemViewModel(Guid id, string value, string? name = null, string? iconUrl = null)
    {
        Id = id;
        Value = value;
        Name = new AppIdNameViewModel(id, name);
        IconUrl = iconUrl;
    }

    public Guid Id { get; }

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    public AppIdNameViewModel Name { get; }

    /// <summary>
    /// Where Steam serves the icon; <see cref="IconPath"/> is its downloaded copy.
    /// </summary>
    public string? IconUrl { get; set; }

    [ObservableProperty]
    private string? iconPath;

    public static AppIdItemViewModel From(AppId appId) => new(appId.Id, appId.Value, appId.Name, appId.IconUrl);
}
