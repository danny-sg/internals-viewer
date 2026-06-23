using System;
using System.Drawing;

namespace InternalsViewer.UI.App.ViewModels.Query;

internal static class ColourConstants
{
    public static readonly Color IoColour = Color.FromArgb(0, 97, 176, 227);
    public static readonly Color PageColour = Color.FromArgb(0, 96, 226, 154);
    public static readonly Color LockColour = Color.FromArgb(0, 249, 120, 120);
    public static readonly Color WaitColour = Color.FromArgb(0, 236, 249, 119);

    // Operator category colours (full alpha). Data access reuses the existing IO blue.
    public static readonly Color DataAccessColour = Color.FromArgb(255, 97, 176, 227);
    public static readonly Color JoinColour = Color.FromArgb(255, 96, 200, 120);
    public static readonly Color TransformationColour = Color.FromArgb(255, 232, 150, 70);
    public static readonly Color BufferColour = Color.FromArgb(255, 170, 120, 220);

    // Transaction-log / data-modification red (distinct from the Lock salmon). Used for the Log lane,
    // log event markers, and modification operators.
    public static readonly Color LogColour = Color.FromArgb(255, 214, 48, 49);

    public static readonly Color SystemIoColour = Desaturate(IoColour, 0.20);
    public static readonly Color SystemPageColour = Desaturate(PageColour, 0.20);
    public static readonly Color SystemLockColour = Desaturate(LockColour, 0.20);
    public static readonly Color SystemWaitColour = Desaturate(WaitColour, 0.20);

    /// <summary>
    /// Reduces saturation to <paramref name="amount"/> (0 = full grey, 1 = original).
    /// Uses a perceptual grey blend: grey = 0.299R + 0.587G + 0.114B.
    /// </summary>
    private static Color Desaturate(Color c, double amount)
    {
        var grey = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
        var r = (int)(grey + amount * (c.R - grey));
        var g = (int)(grey + amount * (c.G - grey));
        var b = (int)(grey + amount * (c.B - grey));
        return Color.FromArgb(c.A, Clamp(r), Clamp(g), Clamp(b));
    }

    private static int Clamp(int v) => Math.Clamp(v, 0, 255);
}