namespace GreenLumaPresets.Models;

public class AppId
{
    public Guid Id { get; set; }
    public Guid PresetId { get; set; }
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Order of the AppID inside its preset.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Display name, filled from Steam or typed by the user.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// URL of the small icon Steam shows in the library, when the app has one.
    /// </summary>
    public string? IconUrl { get; set; }

    public Preset? Preset { get; set; }
}
