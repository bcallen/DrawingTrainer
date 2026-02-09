using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using DrawingTrainer.Models;

namespace DrawingTrainer.Converters;

public class ExerciseGroupConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ICollection<SessionExercise> exercises || exercises.Count == 0)
            return Array.Empty<string>();

        var ordered = exercises.OrderBy(e => e.SortOrder).ToList();
        var result = new List<string>();

        for (int i = 0; i < ordered.Count;)
        {
            var current = ordered[i];
            int count = 1;
            while (i + count < ordered.Count
                && ordered[i + count].TagId == current.TagId
                && ordered[i + count].DurationSeconds == current.DurationSeconds)
            {
                count++;
            }

            var ts = TimeSpan.FromSeconds(current.DurationSeconds);
            var timeStr = ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
                : $"{ts.Seconds}s";

            var tagName = current.Tag?.Name ?? "Unknown";
            var label = count > 1
                ? $"{tagName} - {timeStr} x{count}"
                : $"{tagName} - {timeStr}";

            result.Add(label);
            i += count;
        }

        return result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
