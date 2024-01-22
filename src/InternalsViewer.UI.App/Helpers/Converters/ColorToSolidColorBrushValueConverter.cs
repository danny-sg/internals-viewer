using Microsoft.UI.Xaml.Data;
using System;
using System.Drawing;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.Internals.Engine.Parsers;

namespace InternalsViewer.UI.App.Helpers.Converters;

public class ColorToSolidColorBrushValueConverter : IValueConverter
{
    /// <summary>
    /// Converts a Color (System.Drawing or Windows.UI) to a SolidColorBrush
    /// </summary>
    public object? Convert(object? value, Type targetType, object parameter, string language)
    {
        if (value is null)
        {
            return null;
        }

        if (value is Color drawingColour)
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(drawingColour.A,
                                                                 drawingColour.R,
                                                                 drawingColour.G,
                                                                 drawingColour.B));
        }

        if (value is Windows.UI.Color windowsColour)
        {
            return new SolidColorBrush(windowsColour);
        }

        throw new InvalidOperationException(@"Type cannot be converted - {value.GetType()}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PageAddressToStringConverter: IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, string language)
    {

        return value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        if (value?.ToString() is null)
        {
            return null;
        }

        var stringValue = value.ToString();

        if (PageAddressParser.TryParse(stringValue!, out var pageAddress))
        {
            return pageAddress;
        }

        return null;
    }
}