using System.IO;

namespace GreenLumaPresets;

/// <summary>
/// Per-user locations, writable without administrator rights wherever the app is installed.
/// </summary>
public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GreenLumaPresets");

    public static string DatabasePath { get; } = Path.Combine(DataDirectory, "presets.db");

    public static string LogsDirectory { get; } = Path.Combine(DataDirectory, "logs");

    public static string CacheDirectory { get; } = Path.Combine(DataDirectory, "cache");

    /// <summary>
    /// Database location used up to 1.3.x, next to the executable.
    /// </summary>
    public static string LegacyDatabasePath { get; } = Path.Combine(AppContext.BaseDirectory, "presets.db");
}
