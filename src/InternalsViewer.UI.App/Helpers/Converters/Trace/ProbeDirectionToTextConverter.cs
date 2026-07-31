using System;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters.Trace;

public sealed class ProbeDirectionToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is true ? "→ Probe Right" : "→ Probe Left";
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
