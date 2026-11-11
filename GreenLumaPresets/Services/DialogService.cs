using GreenLumaPresets.Views;
using System.Windows;

namespace GreenLumaPresets.Services;

public class DialogService : IDialogService
{
    public void ShowInformation(string title, string message) => Show(title, message, DialogType.Information);

    public void ShowWarning(string title, string message) => Show(title, message, DialogType.Warning);

    public void ShowError(string title, string message) => Show(title, message, DialogType.Error);

    public bool Confirm(string title, string message)
        => Show(title, message, DialogType.Question) == MessageBoxResult.Yes;

    private static MessageBoxResult Show(string title, string message, DialogType dialogType)
    {
        var dialog = new DialogWindow(title, message, dialogType);

        // Without an owner, CenterOwner falls back to the top-left corner and the dialog,
        // absent from the taskbar, can end up hidden behind the main window.
        var owner = WindowOwner.Find();
        if (owner != null && owner != dialog)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }
}

public enum DialogType
{
    Information,
    Warning,
    Error,
    Question
}
