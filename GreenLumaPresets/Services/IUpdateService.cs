namespace GreenLumaPresets.Services;

public interface IUpdateService
{
    void CheckForUpdates(bool showNoUpdateMessage = false);
    string GetCurrentVersion();
}
