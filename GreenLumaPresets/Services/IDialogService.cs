namespace GreenLumaPresets.Services;

public interface IDialogService
{
    void ShowInformation(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);

    /// <summary>
    /// Asks a yes/no question.
    /// </summary>
    /// <returns>true when the user answered yes.</returns>
    bool Confirm(string title, string message);
}
