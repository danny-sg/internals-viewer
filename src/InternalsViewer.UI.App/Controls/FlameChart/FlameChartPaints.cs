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

    public SKFont Font { get; } = new(SKTypeface.Default, 10f);

    public void Apply(bool isDark)
    {
        LaneHeader = isDark ? new SKColor(255, 255, 255, 14) : new SKColor(0, 0, 0, 12);

        Label.Color = isDark ? new SKColor(0xD8, 0xD8, 0xD8) : new SKColor(0x30, 0x30, 0x30);
        Tick.Color = isDark ? new SKColor(0x70, 0x70, 0x70) : new SKColor(0xA8, 0xA8, 0xA8);
        Hover.Color = isDark ? SKColors.White : SKColors.Black;
        Selection.Color = isDark ? new SKColor(255, 200, 0) : new SKColor(0xE0, 0x7A, 0x00);
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
        Font.Dispose();
    }
}
