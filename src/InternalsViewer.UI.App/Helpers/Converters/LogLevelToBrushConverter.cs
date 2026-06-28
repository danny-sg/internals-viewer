using System;
using Windows.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var colour = (LogLevel)value switch
        {
            LogLevel.Trace => Color.FromArgb(255, 50, 200, 50),
            LogLevel.Debug => Color.FromArgb(255, 150, 150, 150),
            LogLevel.Information => Color.FromArgb(255, 100, 180, 255),
            LogLevel.Warning => Color.FromArgb(255, 255, 200, 80),
            LogLevel.Error => Color.FromArgb(255, 255, 80, 80),
            LogLevel.Critical => Color.FromArgb(255, 220, 50, 50),
            _ => Color.FromArgb(255, 255, 255, 255),
        };

        return new SolidColorBrush(colour);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}