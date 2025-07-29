using System.IO;
using System.Text.Json.Serialization;

namespace GreenLumaPresets.Models;

public class Settings
{
    public string? GreenLumaArchiveUrl { get; init; } = string.Empty;
    public string? GreenLumaArchivePath { get; init; } = string.Empty;

    public bool CanInstallGreenLuma { get => !string.IsNullOrEmpty(GreenLumaArchiveUrl); }
    public bool UseLocalGreenLumaArchive 
    { 
        get => !string.IsNullOrEmpty(GreenLumaArchivePath) &&
                File.Exists(GreenLumaArchivePath); 
    }
}