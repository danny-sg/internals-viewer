using Microsoft.UI.Xaml.Data;
using System;
using System.Drawing;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.vNext.Helpers.Converters;

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
