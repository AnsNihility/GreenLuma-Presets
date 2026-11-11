using GreenLumaPresets.Views;
using System.Windows;

namespace GreenLumaPresets.Services;

public class DialogService : IDialogService
{
    public void ShowInformation(string title, string message)
    {
        var dialog = new DialogWindow(title, message, DialogType.Information);
        dialog.ShowDialog();
    }

    public void ShowWarning(string title, string message)
    {
        var dialog = new DialogWindow(title, message, DialogType.Warning);
        dialog.ShowDialog();
    }

    public void ShowError(string title, string message)
    {
        var dialog = new DialogWindow(title, message, DialogType.Error);
        dialog.ShowDialog();
    }

    public bool Confirm(string title, string message)
    {
        var dialog = new DialogWindow(title, message, DialogType.Question);
        dialog.ShowDialog();
        return dialog.Result == MessageBoxResult.Yes;
    }
}

public enum DialogType
{
    Information,
    Warning,
    Error,
    Question
}
