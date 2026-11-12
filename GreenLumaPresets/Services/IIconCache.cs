namespace GreenLumaPresets.Services;

/// <summary>
/// Keeps downloaded app icons on disk so they are fetched only once.
/// </summary>
public interface IIconCache
{
    /// <summary>
    /// Gets the local file of an icon, downloading it if needed.
    /// </summary>
    /// <returns>The file path, or null when the icon can't be downloaded.</returns>
    Task<string?> GetAsync(string url);
}
