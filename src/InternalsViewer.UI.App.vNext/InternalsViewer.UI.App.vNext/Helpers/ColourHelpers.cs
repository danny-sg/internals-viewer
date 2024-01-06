using SkiaSharp;
using System.Drawing;

namespace InternalsViewer.UI.App.vNext.Helpers;

internal static class ColourHelpers
{
    public static SKColor ToSkColor(this Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// Return the background colour for a given colour, used for gradients
    /// </summary>
    public static Color ToBackgroundColour(Color color)
    {
        var red = color.R + 32 > 255 ? 255 : color.R + 32;
        var green = color.G + 32 > 255 ? 255 : color.G + 32;
        var blue = color.B + 32 > 255 ? 255 : color.B + 32;

        return Color.FromArgb(color.A, red, green, blue);
    }

    public static Color SetTransparency(this Color color, int alpha)
    {
        return Color.FromArgb(alpha, color);
    }
}
