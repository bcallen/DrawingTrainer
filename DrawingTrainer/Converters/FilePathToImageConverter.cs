using System.IO;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using DrawingTrainer.Helpers;

namespace DrawingTrainer.Converters;

public class FilePathToImageConverter : IValueConverter
{
    private const int MaxCacheEntries = 30;
    private static readonly Dictionary<(string, int), BitmapSource> _cache = [];
    private static readonly LinkedList<(string, int)> _cacheOrder = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string filePath || string.IsNullOrEmpty(filePath))
            return null;

        if (!File.Exists(filePath))
            return null;

        int decodeWidth = parameter is string p && int.TryParse(p, out var w) ? w : 300;
        var key = (filePath, decodeWidth);

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                // Move to end (most recently used)
                _cacheOrder.Remove(key);
                _cacheOrder.AddLast(key);
                return cached;
            }
        }

        try
        {
            // Single file read: get EXIF orientation and decode in one pass
            byte[] fileBytes = File.ReadAllBytes(filePath);

            int orientation;
            using (var stream = new MemoryStream(fileBytes))
            {
                var frame = BitmapFrame.Create(stream,
                    BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                    BitmapCacheOption.None);
                orientation = ExifHelper.GetExifOrientation(frame);
            }

            BitmapImage bitmap;
            using (var stream = new MemoryStream(fileBytes))
            {
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (orientation >= 5 && orientation <= 8)
                    bitmap.DecodePixelHeight = decodeWidth;
                else
                    bitmap.DecodePixelWidth = decodeWidth;
                bitmap.EndInit();
            }
            bitmap.Freeze();

            var result = ExifHelper.ApplyExifOrientation(bitmap, orientation);
            result.Freeze();

            lock (_cache)
            {
                if (!_cache.ContainsKey(key))
                {
                    _cache[key] = result;
                    _cacheOrder.AddLast(key);

                    // Evict oldest entries
                    while (_cache.Count > MaxCacheEntries && _cacheOrder.First != null)
                    {
                        var oldest = _cacheOrder.First.Value;
                        _cacheOrder.RemoveFirst();
                        _cache.Remove(oldest);
                    }
                }
            }

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
