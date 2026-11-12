using CommunityToolkit.Mvvm.ComponentModel;

namespace GreenLumaPresets.ViewModels;

/// <summary>
/// The name of an AppID, renamed in place apart from the AppID value.
/// </summary>
public partial class AppIdNameViewModel : ObservableObject, IEditable
{
    public AppIdNameViewModel(Guid appId, string? text)
    {
        AppId = appId;
        Text = text ?? string.Empty;
    }

    public Guid AppId { get; }

    [ObservableProperty]
    private string text = string.Empty;

    [ObservableProperty]
    private bool isEditing;
}
