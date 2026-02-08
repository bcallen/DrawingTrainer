using System.Globalization;
using System.Windows.Data;

namespace DrawingTrainer.Converters;

public class TimeSpanFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
                : $"{ts.Seconds}s";
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
