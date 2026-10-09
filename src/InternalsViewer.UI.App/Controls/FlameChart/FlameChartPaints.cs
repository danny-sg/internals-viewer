using System;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

internal sealed class FlameChartPaints : IDisposable
{
    public SKColor LaneHeader { get; private set; } = new(255, 255, 255, 14);

    public SKColor Unknown { get; } = new(128, 128, 128);

    public SKPaint Fill { get; } = new() { Style = SKPaintStyle.Fill };

    public SKPaint Text { get; } = new() { IsAntialias = true };

    public SKPaint Label { get; } = new() { IsAntialias = true, Color = SKColors.LightGray };

    public SKPaint Tick { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = new SKColor(90, 90, 90) };

    public SKPaint Hover { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = SKColors.White };

    public SKPaint Selection { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        Color = new SKColor(255, 200, 0)
    };

    public SKPaint DragFill { get; } = new() { Style = SKPaintStyle.Fill, Color = new SKColor(80, 160, 255, 50) };

    public SKPaint DragStroke { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
        Color = new SKColor(80, 160, 255)
    };

    public SKPaint Face { get; } = new() { Style = SKPaintStyle.Fill, IsAntialias = true };

    public SKPaint Edge { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
        IsAntialias = true,
        Color = new SKColor(0, 0, 0, 90)
    };

    public SKPaint Guide { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = new SKColor(80, 160, 255) };

    public SKPaint LabelBackground { get; } = new() { Style = SKPaintStyle.Fill, Color = new SKColor(32, 32, 32, 200) };

    public SKPaint AreaLine { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
        IsAntialias = true,
        Color = new SKColor(0x80, 0x80, 0x80)
    };

    public SKPaint InUseLine { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f,
        IsAntialias = true,
        Color = new SKColor(0, 103, 192)
    };

    public SKPaint InUseText { get; } = new() { IsAntialias = true, Color = new SKColor(0, 103, 192) };

    public SKPaint GrantFill { get; } = new() { Style = SKPaintStyle.Fill, Color = new SKColor(0, 103, 192, 28) };

    public SKPaint GrantEdge { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = new SKColor(0, 103, 192, 140) };

    public SKPaint Playhead { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(230, 60, 60) };

    public SKPaint PlayheadFill { get; } = new() { Style = SKPaintStyle.Fill, IsAntialias = true, Color = new SKColor(230, 60, 60) };

    public SKPaint PlayheadText { get; } = new() { IsAntialias = true, Color = SKColors.White };

    public SKPaint Outline { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };

    public SKColor Dim { get; private set; } = new(0, 0, 0, 150);

    public SKFont Font { get; } = new(SKTypeface.Default, 10f);

    public SKFont OperatorFont { get; } = new(SKTypeface.Default, 12f);

    public SKFont OperatorBoldFont { get; } = new(BoldTypeface, 12f);

    private static SKTypeface BoldTypeface { get; } = SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold);

    public void Apply(bool isDark)
    {
        LaneHeader = isDark ? new SKColor(255, 255, 255, 14) : new SKColor(0, 0, 0, 12);

        Label.Color = isDark ? new SKColor(0xD8, 0xD8, 0xD8) : new SKColor(0x30, 0x30, 0x30);
        Tick.Color = isDark ? new SKColor(0x70, 0x70, 0x70) : new SKColor(0xA8, 0xA8, 0xA8);
        Hover.Color = isDark ? SKColors.White : SKColors.Black;
        Selection.Color = isDark ? new SKColor(255, 200, 0) : new SKColor(0xE0, 0x7A, 0x00);
        Edge.Color = isDark ? new SKColor(255, 255, 255, 70) : new SKColor(0, 0, 0, 90);
        Guide.Color = isDark ? new SKColor(110, 180, 255) : new SKColor(0, 103, 192);
        LabelBackground.Color = isDark ? new SKColor(32, 32, 32, 210) : new SKColor(255, 255, 255, 220);
        AreaLine.Color = isDark ? new SKColor(0xA0, 0xA0, 0xA0) : new SKColor(0x80, 0x80, 0x80);
        InUseLine.Color = Guide.Color;
        InUseText.Color = Guide.Color;
        GrantFill.Color = Guide.Color.WithAlpha(isDark ? (byte)40 : (byte)28);
        GrantEdge.Color = Guide.Color.WithAlpha(140);
        Dim = isDark ? new SKColor(0, 0, 0, 150) : new SKColor(255, 255, 255, 170);
    }

    public void Dispose()
    {
        Fill.Dispose();
        Text.Dispose();
        Label.Dispose();
        Tick.Dispose();
        Hover.Dispose();
        Selection.Dispose();
        DragFill.Dispose();
        DragStroke.Dispose();
        Face.Dispose();
        Edge.Dispose();
        Guide.Dispose();
        LabelBackground.Dispose();
        AreaLine.Dispose();
        InUseLine.Dispose();
        InUseText.Dispose();
        GrantFill.Dispose();
        GrantEdge.Dispose();
        Playhead.Dispose();
        PlayheadFill.Dispose();
        PlayheadText.Dispose();
        Outline.Dispose();
        Font.Dispose();
        OperatorFont.Dispose();
        OperatorBoldFont.Dispose();
    }
}
