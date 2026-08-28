using System.Collections.Generic;
using System.Drawing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Point = Windows.Foundation.Point;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace InternalsViewer.UI.App.Helpers;

internal static class IconHighlight
{
    private const double MaximumHighlight = 0.45;

    public static void FillShapes(IReadOnlyList<Rectangle> shapes, Color colour, double iconSize)
    {
        foreach (var shape in shapes)
        {
            shape.Fill = CreateShapeBrush(shape, colour, iconSize);
        }
    }

    private static Brush CreateShapeBrush(Rectangle shape, Color colour, double iconSize)
    {
        var start = Canvas.GetLeft(shape) + Canvas.GetTop(shape);

        var end = start + shape.Width + shape.Height;

        return CreateBrush(colour, start / (iconSize * 2), end / (iconSize * 2));
    }

    public static Brush CreateBrush(Color colour, double start, double end)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };

        brush.GradientStops.Add(new GradientStop { Offset = 0, Color = Highlight(colour, start) });
        brush.GradientStops.Add(new GradientStop { Offset = 1, Color = Highlight(colour, end) });

        return brush;
    }

    private static Windows.UI.Color Highlight(Color colour, double position)
    {
        var strength = MaximumHighlight * (1 - position);

        return Color.FromArgb(colour.A,
                              (byte) (colour.R + ((255 - colour.R) * strength)),
                              (byte) (colour.G + ((255 - colour.G) * strength)),
                              (byte) (colour.B + ((255 - colour.B) * strength)))
                    .ToWindowsColor();
    }
}
