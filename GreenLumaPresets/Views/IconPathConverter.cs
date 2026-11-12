using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GreenLumaPresets.Views;

/// <summary>
/// Loads a cached icon file fully into memory, so the file is never kept locked.
/// </summary>
public class IconPathConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = 32;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception err) when (err is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // A damaged cache file shows no icon rather than breaking the list.
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
