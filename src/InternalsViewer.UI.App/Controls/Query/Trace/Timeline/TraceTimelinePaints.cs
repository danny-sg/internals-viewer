using System;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

internal sealed class TraceTimelinePaints : IDisposable
{
    private const byte MarksAlpha = 204;

    public SKColor LaneHeader { get; private set; } = new(255, 255, 255, 14);

    public SKColor Unknown { get; } = new(128, 128, 128);

    public SKColor Accent { get; private set; } = new(0, 103, 192);

    public SKColor Dim { get; private set; } = new(0, 0, 0, 150);

    public SKColor LatchShared { get; } = new(66, 165, 245);

    public SKColor LatchExclusive { get; } = new(239, 83, 80);

    public SKColor LatchWaiting { get; } = new(255, 167, 38);

    public SKColor LatchReleased { get; } = new(128, 128, 128);

    public SKPaint Fill { get; } = new() { Style = SKPaintStyle.Fill };

    public SKPaint Text { get; } = new() { IsAntialias = true };

    public SKPaint Label { get; } = new() { IsAntialias = true, Color = SKColors.LightGray };

    public SKPaint Tick { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = new SKColor(60, 60, 60) };

    public SKPaint Hover { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = SKColors.White };

    public SKPaint Selection { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(255, 200, 0) };

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

    public SKPaint Mark { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(0x00, 0xC8, 0x53) };

    public SKPaint Marks { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
        Color = new SKColor(0x00, 0xC8, 0x53, MarksAlpha)
    };

    public SKPaint CallLink { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

    public SKPaint DottedCallLink { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
        PathEffect = SKPathEffect.CreateDash([2f, 2f], 0)
    };

    public SKPaint TickHighlight { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(230, 60, 60) };

    public SKPaint SearchMatch { get; } = new() { Style = SKPaintStyle.Fill, Color = new SKColor(255, 214, 0, 191) };

    public SKPaint IndicatorFill { get; } = new()
    {
        Style = SKPaintStyle.Fill,
        Color = new SKColor(255, 255, 255),
        ImageFilter = CreateIndicatorShadow(false)
    };

    public SKPaint IndicatorEdge { get; } = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = GetControlStroke(false) };

    public SKPaint IndicatorCross { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f,
        IsAntialias = true,
        Color = new SKColor(0x30, 0x30, 0x30)
    };

    public SKPaint IndicatorArrow { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 3.5f,
        IsAntialias = true,
        Color = GetGold(false)
    };

    public SKPaint IndicatorHead { get; } = new() { Style = SKPaintStyle.Fill, IsAntialias = true, Color = GetGold(false) };

    public SKPaint IndicatorButton { get; } = new() { Style = SKPaintStyle.Fill, Color = new SKColor(0, 103, 192) };

    public SKPaint IndicatorArrowShadow { get; } = new() { ImageFilter = CreateArrowShadow(false) };

    public SKPaint IndicatorButtonText { get; } = new() { IsAntialias = true, Color = SKColors.White };

    public SKFont Font { get; } = new(SKTypeface.Default, 10f);

    public SKFont HeaderFont { get; } = new(SKTypeface.Default, 11f);

    public SKFont HeaderBoldFont { get; } = new(BoldTypeface, 11f);

    public SKFont TiltedFont { get; } = new(SKTypeface.Default, 10f)
    {
        Edging = SKFontEdging.Antialias,
        Hinting = SKFontHinting.None,
        Subpixel = true,
        BaselineSnap = false,
        LinearMetrics = true
    };

    public SKFont OperatorFont { get; } = new(SKTypeface.Default, 12f);

    public SKFont OperatorBoldFont { get; } = new(BoldTypeface, 12f);

    public SKFont IndicatorFont { get; } = new(SKTypeface.Default, 12f);

    private static SKTypeface BoldTypeface { get; } = SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold);

    public void Apply(bool isDark)
    {
        LaneHeader = isDark ? new SKColor(255, 255, 255, 14) : new SKColor(0, 0, 0, 12);

        Accent = isDark ? new SKColor(110, 180, 255) : new SKColor(0, 103, 192);

        Dim = isDark ? new SKColor(0, 0, 0, 150) : new SKColor(255, 255, 255, 170);

        Label.Color = isDark ? new SKColor(0xD8, 0xD8, 0xD8) : new SKColor(0x30, 0x30, 0x30);
        Tick.Color = isDark ? new SKColor(0x70, 0x70, 0x70) : SKColors.LightGray;
        Hover.Color = isDark ? SKColors.White : SKColors.Black;
        Selection.Color = isDark ? new SKColor(255, 200, 0) : new SKColor(0xE0, 0x7A, 0x00);
        Edge.Color = isDark ? new SKColor(255, 255, 255, 70) : new SKColor(0, 0, 0, 90);
        LabelBackground.Color = isDark ? new SKColor(32, 32, 32, 210) : new SKColor(255, 255, 255, 220);
        AreaLine.Color = isDark ? new SKColor(0xA0, 0xA0, 0xA0) : new SKColor(0x80, 0x80, 0x80);
        InUseLine.Color = Accent;
        InUseText.Color = Accent;
        GrantFill.Color = Accent.WithAlpha(isDark ? (byte)40 : (byte)28);
        GrantEdge.Color = Accent.WithAlpha(140);
        Mark.Color = isDark ? new SKColor(0x00, 0xE6, 0x76) : new SKColor(0x00, 0xC8, 0x53);
        Marks.Color = Mark.Color.WithAlpha(MarksAlpha);
        IndicatorFill.Color = isDark ? new SKColor(43, 43, 43) : new SKColor(255, 255, 255);
        IndicatorFill.ImageFilter?.Dispose();
        IndicatorFill.ImageFilter = CreateIndicatorShadow(isDark);
        IndicatorEdge.Color = GetControlStroke(isDark);
        IndicatorCross.Color = Label.Color;
        IndicatorArrow.Color = GetGold(isDark);
        IndicatorHead.Color = GetGold(isDark);
        IndicatorButton.Color = Accent;
        IndicatorArrowShadow.ImageFilter?.Dispose();
        IndicatorArrowShadow.ImageFilter = CreateArrowShadow(isDark);
        IndicatorButtonText.Color = isDark ? SKColors.Black : SKColors.White;
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
        Mark.Dispose();
        Marks.Dispose();
        CallLink.Dispose();
        DottedCallLink.PathEffect?.Dispose();
        DottedCallLink.Dispose();
        TickHighlight.Dispose();
        SearchMatch.Dispose();
        IndicatorFill.ImageFilter?.Dispose();
        IndicatorFill.Dispose();
        IndicatorEdge.Dispose();
        IndicatorCross.Dispose();
        IndicatorFont.Dispose();
        IndicatorArrow.Dispose();
        IndicatorHead.Dispose();
        IndicatorButton.Dispose();
        IndicatorArrowShadow.ImageFilter?.Dispose();
        IndicatorArrowShadow.Dispose();
        IndicatorButtonText.Dispose();
        HeaderBoldFont.Dispose();
        Font.Dispose();
        TiltedFont.Dispose();
        OperatorFont.Dispose();
        OperatorBoldFont.Dispose();
    }

    private static SKColor GetControlStroke(bool isDark) => isDark ? new SKColor(255, 255, 255, 0x12) : new SKColor(0, 0, 0, 0x0F);

    private static SKColor GetGold(bool isDark) => isDark ? new SKColor(255, 196, 0) : new SKColor(230, 160, 0);

    private static SKImageFilter CreateArrowShadow(bool isDark)
        => SKImageFilter.CreateDropShadow(0, 2, 3, 3, isDark ? new SKColor(0, 0, 0, 0xA0) : new SKColor(0, 0, 0, 0x50));

    private static SKImageFilter CreateIndicatorShadow(bool isDark)
        => SKImageFilter.CreateDropShadow(0, 4, 8, 8, isDark ? new SKColor(0, 0, 0, 0x80) : new SKColor(0, 0, 0, 0x38));
}
