namespace GreenLumaPresets.Models;

public class Preset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// AppID whose icon was picked for the preset; null to use the first AppID with an icon.
    /// Not a foreign key: a stale value simply falls back to the automatic icon.
    /// </summary>
    public Guid? IconAppId { get; set; }

    public List<AppId> AppIds { get; set; } = [];
}
