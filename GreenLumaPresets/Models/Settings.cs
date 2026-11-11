using System.IO;

namespace GreenLumaPresets.Models;

public class Settings
{
    public string? GreenLumaArchiveUrl { get; init; } = string.Empty;
    public string? GreenLumaArchivePath { get; init; } = string.Empty;

    public bool CanInstallGreenLuma { get => !string.IsNullOrEmpty(GreenLumaArchiveUrl); }

    /// <summary>
    /// Archive path resolved against the executable directory when relative.
    /// </summary>
    public string ResolvedGreenLumaArchivePath
    {
        get => string.IsNullOrEmpty(GreenLumaArchivePath)
            ? string.Empty
            : Path.GetFullPath(GreenLumaArchivePath, AppContext.BaseDirectory);
    }

    public bool UseLocalGreenLumaArchive
    {
        get => !string.IsNullOrEmpty(ResolvedGreenLumaArchivePath) &&
                File.Exists(ResolvedGreenLumaArchivePath);
    }
}
