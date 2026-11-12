using System.Windows;

namespace GreenLumaPresets.Services;

public class ClipboardService : IClipboardService
{
    public string GetText() => Clipboard.GetText();
}
