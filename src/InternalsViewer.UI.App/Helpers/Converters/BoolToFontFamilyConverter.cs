using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers.Converters;

public sealed class BoolToFontFamilyConverter : IValueConverter
{
    private static readonly FontFamily Monospace = new("Consolas");

    public object Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is true ? Monospace : FontFamily.XamlAutoFontFamily;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
