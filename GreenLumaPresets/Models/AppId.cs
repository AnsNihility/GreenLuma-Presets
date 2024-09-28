namespace GreenLumaPresets.Models;

public class AppId
{
    public Guid Id { get; set; }
    public Guid PresetId { get; set; }
    public string Value { get; set; }

    public AppId() { }
}
