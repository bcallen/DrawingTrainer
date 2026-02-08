using System.IO;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

            int orientation = GetExifOrientation(frame);

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

            var result = ApplyExifOrientation(bitmap, orientation);
            result.Freeze();
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static int GetExifOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata)
            {
                var orientationObj = metadata.GetQuery("/app1/ifd/{ushort=274}");
                if (orientationObj is ushort orientation)
                    return orientation;
            }
        }
        catch { }
        return 1; // Normal
    }

    private static BitmapSource ApplyExifOrientation(BitmapSource source, int orientation)
    {
        // EXIF orientation values:
        // 1 = Normal
        // 2 = Flipped horizontally
        // 3 = Rotated 180
        // 4 = Flipped vertically
        // 5 = Transposed (rotated 90 CW + flipped horizontally)
        // 6 = Rotated 90 CW
        // 7 = Transverse (rotated 90 CCW + flipped horizontally)
        // 8 = Rotated 90 CCW

        if (orientation == 1)
            return source;

        var transform = orientation switch
        {
            2 => new TransformGroup { Children = { new ScaleTransform(-1, 1) } },
            3 => new TransformGroup { Children = { new RotateTransform(180) } },
            4 => new TransformGroup { Children = { new ScaleTransform(1, -1) } },
            5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
            6 => new TransformGroup { Children = { new RotateTransform(90) } },
            7 => new TransformGroup { Children = { new RotateTransform(-90), new ScaleTransform(-1, 1) } },
            8 => new TransformGroup { Children = { new RotateTransform(-90) } },
            _ => (Transform?)null
        };

        if (transform == null)
            return source;

        var transformed = new TransformedBitmap(source, transform);
        return transformed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
