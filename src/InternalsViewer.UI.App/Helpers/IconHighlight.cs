using System.Drawing;
using Microsoft.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace InternalsViewer.UI.App.Helpers;

/// <summary>
/// Diagonal white highlight shared by the generated command bar icons
/// </summary>
/// <remarks>
/// Each shape takes its two stops from one ramp running top left to bottom right across the whole icon, so shapes drawn
/// separately still read as a single gradient. Positions are the shape's top left and bottom right corners measured
/// along that ramp, where 0 is the top left of the icon and 1 the bottom right.
/// </remarks>
internal static class IconHighlight
{
    private const double MaximumHighlight = 0.65;

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
