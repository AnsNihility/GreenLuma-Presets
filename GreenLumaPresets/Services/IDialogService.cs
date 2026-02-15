using System.Windows;

namespace GreenLumaPresets.Services;

public interface IDialogService
{
    void ShowInformation(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    MessageBoxResult ShowQuestion(string title, string message);
}
