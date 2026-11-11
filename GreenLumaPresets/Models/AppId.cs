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

    public Preset? Preset { get; set; }
}
