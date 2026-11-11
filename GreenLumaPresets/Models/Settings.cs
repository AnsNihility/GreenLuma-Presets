using System.IO;

namespace GreenLumaPresets.Models;

public class Settings
{
    public string? GreenLumaArchiveUrl { get; init; } = string.Empty;
    /// <summary>
    /// Defaults to GreenLuma.rar next to the executable so the app works without any appsettings.json.
    /// </summary>
    public string? GreenLumaArchivePath { get; init; } = "GreenLuma.rar";

    /// <summary>
    /// Optional SHA-256 (hex) the GreenLuma archive must match before being extracted into the Steam folder.
    /// </summary>
    public string? GreenLumaArchiveSha256 { get; init; } = string.Empty;

    /// <summary>
    /// Maximum number of AppIDs supported by the installed GreenLuma version.
    /// A confirmation is asked before loading a bigger preset. Null or 0 disables the check.
    /// </summary>
    public int? MaxAppListSize { get; init; }

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
