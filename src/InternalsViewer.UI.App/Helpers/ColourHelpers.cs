using SkiaSharp;
using System;
using System.Collections.Generic;
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

        double lo = baseL - halfSpread;
        double hi = baseL + halfSpread;

        if (lo < minL) { hi += minL - lo; lo = minL; }
        if (hi > maxL) { lo -= hi - maxL; hi = maxL; }

        lo = Math.Max(lo, minL);
        hi = Math.Min(hi, maxL);

        double t = (double)index / (count - 1);
        double l = LchColorScale.Lerp(lo, hi, t);

        // Keep the base hue and chroma; the gamut-safe conversion eases chroma back only where a
        // given lightness can't display it, which naturally softens the lightest/darkest members.
        return LchColorScale.LchToRgbSafe(l, baseC, baseH);
    }

}

public static class LchColorScale
{
    internal static Color LchToRgbSafe(double L, double C, double H)
    {
        for (int i = 0; i < 20; i++)
        {
            var lab = LchToLab((L, C, H));
            var rgb = LabToRgbRaw(lab);

            if (IsValidRgb(rgb))
                return ToColor(rgb);

            // reduce chroma progressively
            C *= 0.85;
        }

        // fallback (very desaturated but safe)
        var fallback = LchToLab((L, 0, H));
        return ToColor(LabToRgbRaw(fallback));
    }

    static bool IsValidRgb((double r, double g, double b) rgb)
    {
        return rgb.r >= 0 && rgb.r <= 1 &&
               rgb.g >= 0 && rgb.g <= 1 &&
               rgb.b >= 0 && rgb.b <= 1;
    }

    static Color ToColor((double r, double g, double b) rgb)
    {
        return Color.FromArgb(
            Clamp(rgb.r * 255),
            Clamp(rgb.g * 255),
            Clamp(rgb.b * 255));
    }

    static int Clamp(double v)
    {
        return (int)Math.Max(0, Math.Min(255, Math.Round(v)));
    }

    public static List<Color> Generate(Color baseColor, int steps)
    {
        var lab = RgbToLab(baseColor);
        var lch = LabToLch(lab);

        var results = new List<Color>();

        double baseHue = lch.h;
        double startL = Math.Max(25, lch.l - 20);
        double endL = Math.Min(85, lch.l + 25);

        for (int i = 0; i < steps; i++)
        {
            double t = (double)i / (steps - 1);

            double L = Lerp(startL, endL, t);


            double maxSafeChroma = GetSafeChroma(lch.h, L);
            double C = Math.Min(
                maxSafeChroma,
                lch.c * (0.5 + 0.5 * Math.Sin(Math.PI * t))
            );


            double H = baseHue;

            var labColor = LchToLab((L, C, H));
            var rgb = LabToRgb(labColor);

            results.Add(rgb);
        }

        return results;
    }

    internal static double Lerp(double a, double b, double t) => a + (b - a) * t;

    internal static (double l, double a, double b) RgbToLab(Color c)
    {
        var xyz = RgbToXyz(c);

        return XyzToLab(xyz);
    }

    internal static Color LabToRgb((double l, double a, double b) lab)
    {
        var xyz = LabToXyz(lab);

        return XyzToRgb(xyz);
    }

    static (double x, double y, double z) RgbToXyz(Color c)
    {
        double r = PivotRgb(c.R / 255.0);
        double g = PivotRgb(c.G / 255.0);
        double b = PivotRgb(c.B / 255.0);

        r *= 100; 
        g *= 100; 
        b *= 100;

        return (
            r * 0.4124 + g * 0.3576 + b * 0.1805,
            r * 0.2126 + g * 0.7152 + b * 0.0722,
            r * 0.0193 + g * 0.1192 + b * 0.9505
        );
    }

    static (double l, double a, double b) XyzToLab((double x, double y, double z) xyz)
    {
        double x = PivotXyz(xyz.x / 95.047);
        double y = PivotXyz(xyz.y / 100.000);
        double z = PivotXyz(xyz.z / 108.883);

        return (
            116 * y - 16,
            500 * (x - y),
            200 * (y - z)
        );
    }

    static (double x, double y, double z) LabToXyz((double l, double a, double b) lab)
    {
        double y = (lab.l + 16) / 116;
        double x = lab.a / 500 + y;
        double z = y - lab.b / 200;

        return (
            95.047 * InversePivotXyz(x),
            100.000 * InversePivotXyz(y),
            108.883 * InversePivotXyz(z)
        );
    }

    static Color XyzToRgb((double x, double y, double z) xyz)
    {
        double xN = xyz.x / 100;
        double yN = xyz.y / 100;
        double zN = xyz.z / 100;

        double r = xN * 3.2406 + yN * -1.5372 + zN * -0.4986;
        double g = xN * -0.9689 + yN * 1.8758 + zN * 0.0415;
        double b = xN * 0.0557 + yN * -0.2040 + zN * 1.0570;

        r = InversePivotRgb(r);
        g = InversePivotRgb(g);
        b = InversePivotRgb(b);

        return Color.FromArgb(
            ClampToByte(r * 255),
            ClampToByte(g * 255),
            ClampToByte(b * 255)
        );
    }

    internal static (double l, double c, double h) LabToLch((double l, double a, double b) lab)
    {
        double c = Math.Sqrt(lab.a * lab.a + lab.b * lab.b);
        double h = Math.Atan2(lab.b, lab.a) * (180 / Math.PI);

        if (h < 0)
        {
            h += 360;
        }

        return (lab.l, c, h);
    }

    internal static (double l, double a, double b) LchToLab((double l, double c, double h) lch)
    {
        double hRad = lch.h * (Math.PI / 180);

        return (
            lch.l,
            Math.Cos(hRad) * lch.c,
            Math.Sin(hRad) * lch.c
        );
    }

    static double PivotRgb(double n)
        => (n > 0.04045) ? Math.Pow((n + 0.055) / 1.055, 2.4) : n / 12.92;

    static double InversePivotRgb(double n)
        => (n > 0.0031308) ? 1.055 * Math.Pow(n, 1.0 / 2.4) - 0.055 : 12.92 * n;

    static double PivotXyz(double n)
        => (n > 0.008856) ? Math.Pow(n, 1.0 / 3.0) : (7.787 * n) + (16.0 / 116);

    static double InversePivotXyz(double n)
    {
        double n3 = Math.Pow(n, 3);

        return (n3 > 0.008856) ? n3 : (n - 16.0 / 116) / 7.787;
    }

    static (double r, double g, double b) LabToRgbRaw((double l, double a, double b) lab)
    {
        // LAB → XYZ
        double y = (lab.l + 16) / 116;
        double x = lab.a / 500 + y;
        double z = y - lab.b / 200;

        x = 95.047 * InversePivotXyz(x);
        y = 100.000 * InversePivotXyz(y);
        z = 108.883 * InversePivotXyz(z);

        // XYZ → linear RGB
        double xN = x / 100.0;
        double yN = y / 100.0;
        double zN = z / 100.0;

        double rLin = xN * 3.2406 + yN * -1.5372 + zN * -0.4986;
        double gLin = xN * -0.9689 + yN * 1.8758 + zN * 0.0415;
        double bLin = xN * 0.0557 + yN * -0.2040 + zN * 1.0570;

        // Linear → gamma-corrected (sRGB)
        double r = InversePivotRgb(rLin);
        double g = InversePivotRgb(gLin);
        double b = InversePivotRgb(bLin);

        return (r, g, b);
    }

    static int ClampToByte(double v)
    {
        if (double.IsNaN(v)) return 0;
        return (int)Math.Max(0, Math.Min(255, Math.Round(v)));
    }


    static double GetSafeChroma(double h, double l)
    {
        double t = 1 - Math.Abs(2 * (l / 100.0) - 1); 

        return 100 * t; 
    }

}