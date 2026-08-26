using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public class ByteSizeToTextConverter : IValueConverter
{
    private const long Kilobyte = 1024;

    private const long Megabyte = Kilobyte * 1024;

    private const long Gigabyte = Megabyte * 1024;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var bytes = value switch
        {
            long number => number,
            int number => number,
            _ => 0L
        };

        return bytes switch
        {
            >= Gigabyte => $"{bytes / (double)Gigabyte:N1} GB",
            >= Megabyte => $"{bytes / (double)Megabyte:N1} MB",
            >= Kilobyte => $"{bytes / (double)Kilobyte:N0} KB",
            _ => bytes.ToString("N0", CultureInfo.InvariantCulture) + " B"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
