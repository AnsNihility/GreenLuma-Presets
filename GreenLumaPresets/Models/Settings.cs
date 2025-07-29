namespace GreenLumaPresets.Models;

public class Settings
{
    public string? GreenLumaFilesUrlDownload { get; set; }

    public bool CanInstallGreenLuma { get => !string.IsNullOrEmpty(GreenLumaFilesUrlDownload); }
}