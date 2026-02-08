using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DrawingTrainer.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value switch
        {
            bool b => b,
            int i => i != 0,
            _ => false
        };
        var invert = parameter is string s && s == "Invert";
        if (invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
