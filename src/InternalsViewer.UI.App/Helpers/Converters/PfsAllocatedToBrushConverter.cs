using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class PfsAllocatedToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush AllocatedBrush = new(Colors.DarkGreen);

    private static readonly SolidColorBrush NotAllocatedBrush = new(Colors.Maroon);

    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is true ? AllocatedBrush : NotAllocatedBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
