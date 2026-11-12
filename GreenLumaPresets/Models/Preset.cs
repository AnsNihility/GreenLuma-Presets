namespace GreenLumaPresets.Models;

public class Preset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<AppId> AppIds { get; set; } = [];
}
