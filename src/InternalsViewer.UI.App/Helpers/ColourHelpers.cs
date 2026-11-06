using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using SkiaSharp;

namespace InternalsViewer.UI.App.Helpers;

internal static class ColourHelpers
{
    private const double MinimumContrastDistance = 200;

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

    public static Windows.UI.Color ToWindowsColor(this SKColor color)
    {
        return Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }

    public static Color ToColor(this Windows.UI.Color color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    public static Windows.UI.Color? FromHex(string? hex)
        => hex?.TrimStart('#') is { Length: 6 } digits && uint.TryParse(digits, NumberStyles.HexNumber, null, out var rgb)
            ? Windows.UI.Color.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : null;

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

    public static Color GetSeriesColour(Color baseColor, int index, int count)
    {
        if (count <= 1)
        {
            return baseColor;
        }

        var (baseL, baseC, baseH) = LabToLch(RgbToLab(baseColor));

        const double minL = 32, maxL = 86, halfSpread = 22;

        var lo = baseL - halfSpread;
        var hi = baseL + halfSpread;

        if (lo < minL)
        {
            hi += minL - lo; lo = minL;
        }

        if (hi > maxL)
        {
            lo -= hi - maxL; hi = maxL;
        }

        lo = Math.Max(lo, minL);
        hi = Math.Min(hi, maxL);

        var t = (double)index / (count - 1);
        var l = Lerp(lo, hi, t);

        return LchToRgbSafe(l, baseC, baseH);
    }

    public static List<Color> Generate(Color baseColor, int steps)
    {
        var lab = RgbToLab(baseColor);
        var (l, c, baseHue) = LabToLch(lab);

        var results = new List<Color>();

        var startL = Math.Max(25, l - 20);
        var endL = Math.Min(85, l + 25);

        for (var i = 0; i < steps; i++)
        {
            var t = (double)i / (steps - 1);

            var lightness = Lerp(startL, endL, t);

            var maxSafeChroma = GetSafeChroma(baseHue, lightness);

            var chroma = Math.Min(maxSafeChroma, c * (0.5 + 0.5 * Math.Sin(Math.PI * t)));

            var hue = baseHue;

            var labColor = LchToLab((lightness, chroma, hue));
            var rgb = LabToRgb(labColor);

            results.Add(rgb);
        }

        return results;
    }

    internal static Color LchToRgbSafe(double l, double c, double h)
    {
        for (var i = 0; i < 20; i++)
        {
            var lab = LchToLab((l, c, h));
            var rgb = LabToRgbRaw(lab);

            if (IsValidRgb(rgb))
            {
                return ToColor(rgb);
            }

            // reduce chroma progressively
            c *= 0.85;
        }

        // fallback (very desaturated but safe)
        var fallback = LchToLab((l, 0, h));
        return ToColor(LabToRgbRaw(fallback));
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

    internal static (double l, double c, double h) LabToLch((double l, double a, double b) lab)
    {
        var c = Math.Sqrt(lab.a * lab.a + lab.b * lab.b);
        var h = Math.Atan2(lab.b, lab.a) * (180 / Math.PI);

        if (h < 0)
        {
            h += 360;
        }

        return (lab.l, c, h);
    }

    internal static (double l, double a, double b) LchToLab((double l, double c, double h) lch)
    {
        var hRad = lch.h * (Math.PI / 180);

        return (
            lch.l,
            Math.Cos(hRad) * lch.c,
            Math.Sin(hRad) * lch.c
        );
    }

    public static Color ContrastingWith(this Color colour, Color background)
    {
        return Distance(colour, background) < MinimumContrastDistance ? HighContrast(background) : colour;
    }

    public static Color Lighten(Color colour)
    {
        return Color.FromArgb(255,
                              colour.R + (255 - colour.R) * 3 / 4,
                              colour.G + (255 - colour.G) * 3 / 4,
                              colour.B + (255 - colour.B) * 3 / 4);
    }

    /// <summary>
    /// Weighted distance between two colours
    /// </summary>
    /// <remarks>
    /// Weights approximating how strongly the eye reads each channel
    /// </remarks>
    private static double Distance(Color left, Color right)
    {
        var redMean = (left.R + right.R) / 2.0;

        double red = left.R - right.R;
        double green = left.G - right.G;
        double blue = left.B - right.B;

        return Math.Sqrt((((512 + redMean) * red * red) / 256)
                         + (4 * green * green)
                         + (((767 - redMean) * blue * blue) / 256));
    }

    private static Color HighContrast(Color background)
    {
        return Luminance(background) > 0.5 ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(255, 255, 255, 255);
    }

    private static double Luminance(Color colour)
    {
        return ((0.299 * colour.R) + (0.587 * colour.G) + (0.114 * colour.B)) / 255;
    }

    private static bool IsValidRgb((double r, double g, double b) rgb)
    {
        return rgb.r is >= 0 and <= 1 &&
               rgb.g is >= 0 and <= 1 &&
               rgb.b is >= 0 and <= 1;
    }

    private static Color ToColor((double r, double g, double b) rgb)
    {
        return Color.FromArgb(Clamp(rgb.r * 255),
                              Clamp(rgb.g * 255),
                              Clamp(rgb.b * 255));
    }

    private static int Clamp(double v)
    {
        return (int)Math.Max(0, Math.Min(255, Math.Round(v)));
    }

    private static (double x, double y, double z) RgbToXyz(Color c)
    {
        var r = PivotRgb(c.R / 255.0);
        var g = PivotRgb(c.G / 255.0);
        var b = PivotRgb(c.B / 255.0);

        r *= 100;
        g *= 100;
        b *= 100;

        return (
            r * 0.4124 + g * 0.3576 + b * 0.1805,
            r * 0.2126 + g * 0.7152 + b * 0.0722,
            r * 0.0193 + g * 0.1192 + b * 0.9505
        );
    }

    private static (double l, double a, double b) XyzToLab((double x, double y, double z) xyz)
    {
        var x = PivotXyz(xyz.x / 95.047);
        var y = PivotXyz(xyz.y / 100.000);
        var z = PivotXyz(xyz.z / 108.883);

        return (
            116 * y - 16,
            500 * (x - y),
            200 * (y - z)
        );
    }

    private static (double x, double y, double z) LabToXyz((double l, double a, double b) lab)
    {
        var y = (lab.l + 16) / 116;
        var x = lab.a / 500 + y;
        var z = y - lab.b / 200;

        return (
            95.047 * InversePivotXyz(x),
            100.000 * InversePivotXyz(y),
            108.883 * InversePivotXyz(z)
        );
    }

    private static Color XyzToRgb((double x, double y, double z) xyz)
    {
        var xN = xyz.x / 100;
        var yN = xyz.y / 100;
        var zN = xyz.z / 100;

        var r = xN * 3.2406 + yN * -1.5372 + zN * -0.4986;
        var g = xN * -0.9689 + yN * 1.8758 + zN * 0.0415;
        var b = xN * 0.0557 + yN * -0.2040 + zN * 1.0570;

        r = InversePivotRgb(r);
        g = InversePivotRgb(g);
        b = InversePivotRgb(b);

        return Color.FromArgb(
            ClampToByte(r * 255),
            ClampToByte(g * 255),
            ClampToByte(b * 255)
        );
    }

    private static double PivotRgb(double n)
        => (n > 0.04045) ? Math.Pow((n + 0.055) / 1.055, 2.4) : n / 12.92;

    private static double InversePivotRgb(double n)
        => (n > 0.0031308) ? 1.055 * Math.Pow(n, 1.0 / 2.4) - 0.055 : 12.92 * n;

    private static double PivotXyz(double n)
        => (n > 0.008856) ? Math.Pow(n, 1.0 / 3.0) : (7.787 * n) + (16.0 / 116);

    private static double InversePivotXyz(double n)
    {
        var n3 = Math.Pow(n, 3);

        return (n3 > 0.008856) ? n3 : (n - 16.0 / 116) / 7.787;
    }

    private static (double r, double g, double b) LabToRgbRaw((double l, double a, double b) lab)
    {
        // LAB → XYZ
        var y = (lab.l + 16) / 116;
        var x = lab.a / 500 + y;
        var z = y - lab.b / 200;

        x = 95.047 * InversePivotXyz(x);
        y = 100.000 * InversePivotXyz(y);
        z = 108.883 * InversePivotXyz(z);

        // XYZ → linear RGB
        var xN = x / 100.0;
        var yN = y / 100.0;
        var zN = z / 100.0;

        var rLin = xN * 3.2406 + yN * -1.5372 + zN * -0.4986;
        var gLin = xN * -0.9689 + yN * 1.8758 + zN * 0.0415;
        var bLin = xN * 0.0557 + yN * -0.2040 + zN * 1.0570;

        // Linear → gamma-corrected (sRGB)
        var r = InversePivotRgb(rLin);
        var g = InversePivotRgb(gLin);
        var b = InversePivotRgb(bLin);

        return (r, g, b);
    }

    private static int ClampToByte(double v)
    {
        if (double.IsNaN(v))
        {
            return 0;
        }

        return (int)Math.Max(0, Math.Min(255, Math.Round(v)));
    }

    private static double GetSafeChroma(double h, double l)
    {
        var t = 1 - Math.Abs(2 * (l / 100.0) - 1);

        return 100 * t;
    }
}