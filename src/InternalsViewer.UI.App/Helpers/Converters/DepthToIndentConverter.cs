using System;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class DepthToIndentConverter : IValueConverter
{
    private const double IndentWidth = 12;

    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        var top = double.TryParse(parameter as string, out var parsed) ? parsed : 0;

        return new Thickness(value is int depth ? depth * IndentWidth : 0, top, 0, 0);
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
