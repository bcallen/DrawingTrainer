using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DrawingTrainer.Helpers;

public static class ExifHelper
{
    public static int GetExifOrientation(BitmapFrame frame)
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

    public static BitmapSource ApplyExifOrientation(BitmapSource source, int orientation)
    {
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
}
