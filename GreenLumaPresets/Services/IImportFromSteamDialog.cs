namespace GreenLumaPresets.Services;

public interface IImportFromSteamDialog
{
    /// <summary>
    /// Lets the user pick a Steam app.
    /// </summary>
    /// <returns>The app and its DLC, or null when cancelled.</returns>
    SteamApp? Show();
}
