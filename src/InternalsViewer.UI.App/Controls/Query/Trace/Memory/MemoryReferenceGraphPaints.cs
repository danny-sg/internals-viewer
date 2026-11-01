using System;
using System.Collections.Generic;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Memory;

internal sealed class MemoryReferenceGraphPaints : IDisposable
{
    private const float DashLength = 4;

    private const float DashGap = 3;

    public SKColor Line { get; set; } = new(0x8A, 0x8A, 0x8A);

    public SKColor CrossLine { get; set; } = new(0xB8, 0xB8, 0xB8);

    public SKColor HoverLine { get; set; } = new(0x0F, 0x7B, 0x0F);

    public SKColor HotLine { get; set; } = new(0xC2, 0x5E, 0x00);

    public SKColor Progress { get; set; } = new(0x80, 0x80, 0x80, 0x59);

    public SKColor Active { get; set; } = new(0x60, 0x60, 0x60);

    public SKColor Node { get; set; } = new(0xF9, 0xF9, 0xF9);

    public SKColor DataNode { get; set; } = new(0xDF, 0xF6, 0xDD);

    public SKColor Accent { get; set; } = new(0x00, 0x5F, 0xB8);

    public SKColor Text { get; set; } = SKColors.Black;

    public SKColor SecondaryText { get; set; } = new(0x60, 0x60, 0x60);

    public SKColor DisabledText { get; set; } = new(0xA0, 0xA0, 0xA0);

    public SKColor Link { get; set; } = new(0x00, 0x3E, 0x92);

    public SKColor HoverFill { get; set; } = new(0x00, 0x00, 0x00, 0x09);

    public SKColor DragFill { get; } = new(80, 160, 255, 50);

    public SKColor DragStroke { get; } = new(80, 160, 255);

    public SKPaint Fill { get; } = new() { Style = SKPaintStyle.Fill, IsAntialias = false };

    public SKPaint Stroke { get; } = new() { Style = SKPaintStyle.Stroke, IsAntialias = false };

    public SKPaint Shape { get; } = new() { Style = SKPaintStyle.Fill, IsAntialias = true };

    public SKPaint Glyph { get; } = new() { Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };

    public SKPaint TextPaint { get; } = new() { IsAntialias = true };

    public SKFont NameFont { get; } = CreateFont(TextTypeface);

    public SKFont DetailFont { get; } = CreateFont(MonoTypeface);

    public SKFont LabelFont { get; } = CreateFont(MonoTypeface);

    private static SKTypeface TextTypeface { get; } = SKTypeface.FromFamilyName("Segoe UI Variable Text") ?? SKTypeface.Default;

    private static SKTypeface MonoTypeface { get; } = SKTypeface.FromFamilyName("Consolas") ?? SKTypeface.Default;

    private Dictionary<(float Scale, float Phase), SKPathEffect> Dashes { get; } = [];

    public float GetDashPeriod(float scale) => GetDashLength(scale) + GetDashGap(scale);

    public SKPathEffect GetDash(float scale, float phase)
    {
        if (!Dashes.TryGetValue((scale, phase), out var dash))
        {
            dash = SKPathEffect.CreateDash([GetDashLength(scale), GetDashGap(scale)], phase);

            Dashes[(scale, phase)] = dash;
        }

        return dash;
    }

    public void Dispose()
    {
        Fill.Dispose();

        Stroke.Dispose();

        Shape.Dispose();

        Glyph.Dispose();

        TextPaint.Dispose();

        NameFont.Dispose();

        DetailFont.Dispose();

        LabelFont.Dispose();

        foreach (var dash in Dashes.Values)
        {
            dash.Dispose();
        }

        Dashes.Clear();
    }

    private static float GetDashLength(float scale) => Math.Max(1, MathF.Round(DashLength * scale));

    private static float GetDashGap(float scale) => Math.Max(1, MathF.Round(DashGap * scale));

    private static SKFont CreateFont(SKTypeface typeface) => new(typeface)
    {
        Edging = SKFontEdging.Antialias,
        Hinting = SKFontHinting.Normal,
        Subpixel = true,
        BaselineSnap = true
    };
}
