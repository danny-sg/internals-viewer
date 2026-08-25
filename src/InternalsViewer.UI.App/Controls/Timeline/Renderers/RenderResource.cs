using System;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// The timeline's shared SkiaSharp paints, owned in one place and disposed by the control
/// </summary>
/// <remarks>
/// Paints are addressed by structural role, not reused across modes: a caller sets only the colour (and, for a stroke,
/// its width) before drawing and never flips <c>Style</c> on a shared instance. That is what lets several renderers
/// share these without one leaving a paint in a state another depends on, so no save/restore of paint state is needed.
/// </remarks>
internal sealed class RenderResource : IDisposable
{
    private readonly SKPathEffect _callRailDash = SKPathEffect.CreateDash([2f, 2f], 0f);

    public RenderResource()
    {
        ReadCallRail = new SKPaint
        {
            StrokeWidth = 1f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = false,
            PathEffect = _callRailDash,
        };
    }

    // Crisp (non-antialiased) fill for pixel-aligned bars and ticks; the caller sets Color per draw.
    public SKPaint Fill { get; } = new() { Style = SKPaintStyle.Fill };

    // Antialiased fill for shapes with sloped edges (carets, triangles); the caller sets Color per draw.
    public SKPaint AntialiasedFill { get; } = new() { Style = SKPaintStyle.Fill, IsAntialias = true };

    // Antialiased stroke for marker outlines; the caller sets Color and StrokeWidth per draw.
    public SKPaint Stroke { get; } = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeJoin = SKStrokeJoin.Round,
    };

    // The trace overlay is composited through this one translucent layer (SaveLayer paint) so overlapping rails merge
    // once instead of stacking to full opacity.
    public SKPaint TraceLayer { get; } = new() { Color = SKColors.White.WithAlpha(90) };

    // The dotted call rail dropping from an operator to the top of its read; the caller sets Color per draw.
    public SKPaint ReadCallRail { get; }

    // The solid per-page return rail dropping through the read's full height; the caller sets Color per draw.
    public SKPaint ReadReturnRail { get; } = new() { StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = false };

    // The chrome's label font and paint (row labels, ruler ticks, playhead badge), shared across the chrome renderers.
    public SKFont LabelFont { get; } = new(SKTypeface.Default, 10f);

    public SKPaint LabelPaint { get; } = new() { Color = SKColors.LightGray, IsAntialias = true };

    public void Dispose()
    {
        Fill.Dispose();
        AntialiasedFill.Dispose();
        Stroke.Dispose();
        TraceLayer.Dispose();
        ReadCallRail.Dispose();
        ReadReturnRail.Dispose();
        LabelFont.Dispose();
        LabelPaint.Dispose();
        _callRailDash.Dispose();
    }
}
