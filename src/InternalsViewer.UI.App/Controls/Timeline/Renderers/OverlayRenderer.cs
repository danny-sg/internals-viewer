using System;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// The per-frame interaction state the overlay draws: the from/to selection window, the range-handle draw positions,
/// and the playhead
/// </summary>
internal readonly record struct TimelineOverlay(bool SelectionActive,
                                                float SelectionLoX,
                                                float SelectionHiX,
                                                float StartHandleX,
                                                float EndHandleX,
                                                float PlayheadX,
                                                double PlayheadMs);

/// <summary>
/// Draws the dynamic overlay above the cached static layer: the selection dim outside the from/to window, the two range
/// handles, and the playhead (line, triangle and time badge)
/// </summary>
/// <remarks>
/// Unlike the lane renderers this is driven by live interaction state, passed in as a <see cref="TimelineOverlay"/>, not
/// the static frame. The visual layout constants mirror the control's hit-test geometry — a candidate to hoist into a
/// shared metrics type so drawing and hit-testing can't drift.
/// </remarks>
internal sealed class OverlayRenderer(RenderResource resources) : IDisposable
{
    private const float RowLabelWidth = 36f;
    private const float RulerBandHeight = 18f;
    private const float HandleBandHeight = 16f;
    private const float MarkerStripHeight = RulerBandHeight + HandleBandHeight;
    private const float HandleWidth = 7f;
    private const float HandleHeight = 8f;
    private const float TriangleHalfWidth = 9f;

    private static readonly SKColor PlayheadColour = new(230, 60, 60);
    private static readonly SKColor HandleColour = new(95, 95, 95);

    private readonly SKPaint _playhead = new()
    {
        Color = PlayheadColour,
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    private readonly SKPaint _playheadFill = new() { Color = PlayheadColour, Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKPaint _handle = new() { Color = HandleColour, Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKPaint _clipDim = new() { Color = new SKColor(0, 0, 0, 120), Style = SKPaintStyle.Fill };

    private readonly SKPaint _badgeText = new() { Color = SKColors.White, IsAntialias = true };

    private readonly SKPathBuilder _pathBuilder = new();

    public void Draw(SKCanvas canvas, float w, float h, TimelineOverlay overlay)
    {
        canvas.Save();

        canvas.ClipRect(new SKRect(RowLabelWidth, 0, w, h));

        if (overlay.SelectionActive)
        {
            var rowsTop = MarkerStripHeight;
            var rowsHeight = h - rowsTop;

            if (overlay.SelectionLoX > RowLabelWidth)
            {
                canvas.DrawRect(RowLabelWidth, rowsTop, overlay.SelectionLoX - RowLabelWidth, rowsHeight, _clipDim);
            }

            if (overlay.SelectionHiX < w)
            {
                canvas.DrawRect(overlay.SelectionHiX, rowsTop, w - overlay.SelectionHiX, rowsHeight, _clipDim);
            }
        }

        DrawHandle(canvas, overlay.StartHandleX, isStart: true);
        DrawHandle(canvas, overlay.EndHandleX, isStart: false);

        canvas.DrawLine(overlay.PlayheadX, MarkerStripHeight, overlay.PlayheadX, h, _playhead);

        DrawPlayheadTriangle(canvas, overlay.PlayheadX);

        DrawBadge(canvas, w, overlay.PlayheadX, overlay.PlayheadMs);

        canvas.Restore();
    }

    public void Dispose()
    {
        _playhead.Dispose();
        _playheadFill.Dispose();
        _handle.Dispose();
        _clipDim.Dispose();
        _badgeText.Dispose();
        _pathBuilder.Dispose();
    }

    private void DrawHandle(SKCanvas canvas, float x, bool isStart)
    {
        var top = MarkerStripHeight - HandleHeight;
        var half = HandleWidth / 2f;

        _pathBuilder.MoveTo(x - half, top);
        _pathBuilder.LineTo(x + half, top);
        _pathBuilder.LineTo(isStart ? x - half : x + half, MarkerStripHeight);
        _pathBuilder.Close();

        using var path = _pathBuilder.Detach();

        canvas.DrawPath(path, _handle);
    }

    private void DrawPlayheadTriangle(SKCanvas canvas, float x)
    {
        _pathBuilder.MoveTo(x, MarkerStripHeight);
        _pathBuilder.LineTo(x - TriangleHalfWidth, RulerBandHeight);
        _pathBuilder.LineTo(x + TriangleHalfWidth, RulerBandHeight);
        _pathBuilder.Close();

        using var path = _pathBuilder.Detach();

        canvas.DrawPath(path, _playheadFill);
    }

    private void DrawBadge(SKCanvas canvas, float w, float px, double playheadMs)
    {
        Span<char> buf = stackalloc char[12];

        var textLength = TimelineFormat.FormatTimeIntoSpan(playheadMs, buf);

        var text = buf[..textLength];

        const float padding = 4f;

        var badgeWidth = resources.LabelFont.MeasureText(text) + padding * 2;

        const float badgeHeight = RulerBandHeight - 2;

        var bx = Math.Clamp(px - badgeWidth / 2f, RowLabelWidth, Math.Max(RowLabelWidth, w - badgeWidth));

        canvas.DrawRoundRect(new SKRect(bx, 0, bx + badgeWidth, badgeHeight), 2, 2, _playheadFill);

        var baseline = badgeHeight / 2f + resources.LabelFont.Size * 0.35f;

        using var blob = SKTextBlob.Create(text, resources.LabelFont, SKPoint.Empty);

        if (blob is not null)
        {
            canvas.DrawText(blob, bx + padding, baseline, _badgeText);
        }
    }
}
