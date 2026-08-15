using System;
using System.Drawing;
using SkiaSharp;

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

        var h = (double) hue / 255 * 360 % 360;
        var s = (double) saturation / 255;
        var v = (double) value / 255;

        if (s == 0)
        {
            r = v;
            g = v;
            b = v;
        }
        else
        {
            var sectorPos = h / 60;
            var sectorNumber = (int) Math.Floor(sectorPos);

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

        return Color.FromArgb((int) (r * 255), (int) (g * 255), (int) (b * 255));
    }


    /// <summary>
    /// Produces a perceptually even spread of colours related to <paramref name="baseColor"/>:
    /// the base hue and chroma are preserved while the CIE L*C*h* lightness progresses across the
    /// series, so the members look like a tidy family of shades anchored on the base colour.
    /// </summary>
    /// <param name="baseColor">The colour the series is built around.</param>
    /// <param name="index">Position in the series (0 = darkest, count-1 = lightest).</param>
    /// <param name="count">Total number of colours in the series.</param>
    public static Color GetSeriesColour(Color baseColor, int index, int count)
    {
        if (count <= 1)
        {
            return baseColor;
        }

        var (baseL, baseC, baseH) = LchColorScale.LabToLch(LchColorScale.RgbToLab(baseColor));

        // A lightness window centred on the base. If the base sits near an edge the window is
        // shifted to stay inside the legible range rather than clipped, so the swatches keep an
        // even perceptual spacing instead of bunching up at black/white.
        const double minL = 32, maxL = 86, halfSpread = 22;

        var lo = baseL - halfSpread;
        var hi = baseL + halfSpread;

        if (lo < minL) { hi += minL - lo; lo = minL; }
        if (hi > maxL) { lo -= hi - maxL; hi = maxL; }

        lo = Math.Max(lo, minL);
        hi = Math.Min(hi, maxL);

        var t = (double)index / (count - 1);
        var l = LchColorScale.Lerp(lo, hi, t);

        // Keep the base hue and chroma; the gamut-safe conversion eases chroma back only where a
        // given lightness can't display it, which naturally softens the lightest/darkest members.
        return LchColorScale.LchToRgbSafe(l, baseC, baseH);
    }

    public static Color Lighten(Color colour)
    {
        return Color.FromArgb(255,
                              colour.R + (255 - colour.R) * 3 / 4,
                              colour.G + (255 - colour.G) * 3 / 4,
                              colour.B + (255 - colour.B) * 3 / 4);
    }
}