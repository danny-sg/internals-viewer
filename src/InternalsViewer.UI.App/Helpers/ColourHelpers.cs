using SkiaSharp;
using System;
using System.Drawing;

namespace InternalsViewer.UI.App.Helpers;

internal static class ColourHelpers
{
    public static SKColor ToSkColor(this Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    public static Windows.UI.Color ToWindowsColor(this Color color)
    {
        return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    public static SKColor ToSkColor(this Windows.UI.Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    public static Color ToColor(this Windows.UI.Color color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
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

    public static Color HsvToColor(int hue, int saturation, int value)
    {
        double r = 0;
        double g = 0;
        double b = 0;

        var h = (double)hue / 255 * 360 % 360;
        var s = (double)saturation / 255;
        var v = (double)value / 255;

        if (s == 0)
        {
            r = v;
            g = v;
            b = v;
        }
        else
        {
            var sectorPos = h / 60;
            var sectorNumber = (int)Math.Floor(sectorPos);

            var fractionalSector = sectorPos - sectorNumber;

            var p = v * (1 - s);
            var q = v * (1 - s * fractionalSector);
            var t = v * (1 - s * (1 - fractionalSector));

            switch (sectorNumber)
            {
                case 0:
                    r = v;
                    g = t;
                    b = p;
                    break;

                case 1:
                    r = q;
                    g = v;
                    b = p;
                    break;

                case 2:
                    r = p;
                    g = v;
                    b = t;
                    break;

                case 3:
                    r = p;
                    g = q;
                    b = v;
                    break;

                case 4:
                    r = t;
                    g = p;
                    b = v;
                    break;

                case 5:
                    r = v;
                    g = p;
                    b = q;
                    break;
            }
        }

        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }
}
