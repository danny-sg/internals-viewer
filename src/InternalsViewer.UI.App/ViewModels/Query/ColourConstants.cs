using System;
using System.Drawing;

namespace InternalsViewer.UI.App.ViewModels.Query;

internal static class ColourConstants
{
    public static readonly Color IoColour = Color.FromArgb(0, 97, 176, 227);
    public static readonly Color PageColour = Color.FromArgb(0, 96, 226, 154);
    public static readonly Color LockColour = Color.FromArgb(0, 249, 120, 120);
    public static readonly Color WaitColour = Color.FromArgb(0, 236, 249, 119);
    public static readonly Color LatchColour = Color.FromArgb(255, 210, 170, 248);

    // Operator category colours (full alpha). Data access reuses the existing IO blue.
    public static readonly Color DataAccessColour = Color.FromArgb(255, 97, 176, 227);
    public static readonly Color JoinColour = Color.FromArgb(255, 96, 200, 120);
    public static readonly Color TransformationColour = Color.FromArgb(255, 232, 150, 70);
    public static readonly Color BufferColour = Color.FromArgb(255, 170, 120, 220);

    public static readonly Color LogColour = Color.FromArgb(255, 116, 129, 211);

    public static readonly Color SystemIoColour = Desaturate(IoColour, 0.20);
    public static readonly Color SystemPageColour = Desaturate(PageColour, 0.20);
    public static readonly Color SystemLockColour = Desaturate(LockColour, 0.20);
    public static readonly Color SystemWaitColour = Desaturate(WaitColour, 0.20);
    public static readonly Color SystemLatchColour = Desaturate(LatchColour, 0.20);

    /// <summary>
    /// Reduces saturation from a colour
    /// </summary>
    /// <param name="colour">Colour to desaturate</param>
    /// <param name="amount">Value 0 - 100, 100 = full desaturation</param>
    private static Color Desaturate(Color colour, double amount)
    {
        var grey = (int)(0.299 * colour.R + 0.587 * colour.G + 0.114 * colour.B);
        
        var r = (int)(grey + amount * (colour.R - grey));
        var g = (int)(grey + amount * (colour.G - grey));
        var b = (int)(grey + amount * (colour.B - grey));

        return Color.FromArgb(colour.A, Clamp(r), Clamp(g), Clamp(b));
    }

    private static int Clamp(int v) => Math.Clamp(v, 0, 255);
}