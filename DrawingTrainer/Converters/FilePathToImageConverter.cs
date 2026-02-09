using System.IO;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using DrawingTrainer.Helpers;

namespace DrawingTrainer.Converters;

public class FilePathToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string filePath || string.IsNullOrEmpty(filePath))
            return null;

        if (!File.Exists(filePath))
            return null;

        try
        {
            int decodeWidth = parameter is string p && int.TryParse(p, out var w) ? w : 300;

            // Read EXIF orientation, then produce a correctly-oriented BitmapSource
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var frame = BitmapFrame.Create(stream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);

            int orientation = ExifHelper.GetExifOrientation(frame);

            // Decode a thumbnail-sized version for performance
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            // For rotated images (orientation 5-8), swap width/height for decode
            if (orientation >= 5 && orientation <= 8)
                bitmap.DecodePixelHeight = decodeWidth;
            else
                bitmap.DecodePixelWidth = decodeWidth;
            bitmap.EndInit();
            bitmap.StreamSource.Dispose();
            bitmap.Freeze();

            var result = ExifHelper.ApplyExifOrientation(bitmap, orientation);
            result.Freeze();
            return result;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
