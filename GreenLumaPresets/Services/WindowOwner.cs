using System.Windows;

namespace GreenLumaPresets.Services;

internal static class WindowOwner
{
    /// <summary>
    /// The window a new dialog should belong to, so it opens centered on it and stays in front.
    /// </summary>
    public static Window? Find()
    {
        var application = Application.Current;
        if (application == null) return null;

        return application.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? (application.MainWindow is { IsLoaded: true } mainWindow ? mainWindow : null);
    }
}
