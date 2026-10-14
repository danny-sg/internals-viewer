using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers.Converters.Page;

/// <summary>Converts a CSS-style hex color string (e.g. "#4472C4") to a SolidColorBrush</summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public static Brush ToBrush(string? hex) => new SolidColorBrush(ColourHelpers.FromHex(hex) ?? Colors.Transparent);

    public object Convert(object? value, Type targetType, object parameter, string language) => ToBrush(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
