using System;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Timeline.Renderers;

/// <summary>
/// The per-frame interaction state the overlay draws: the from/to selection window, the range-handle draw positions,
/// and the playhead
/// </summary>
internal readonly record struct TimelineOverlay(bool SelectionActive,
                                                float SelectionLoX,
                                                float SelectionHiX,
                                                float PlayheadX,
                                                float BandLabelWidth);

/// <summary>
/// Draws the dynamic overlay above the cached static layer: the selection dim outside the from/to window, the two range
/// handles, and the playhead (line, triangle and time badge)
/// </summary>
/// <remarks>
/// Unlike the lane renderers this is driven by live interaction state, passed in as a <see cref="TimelineOverlay"/>, not
/// the static frame. The visual layout constants mirror the control's hit-test geometry — a candidate to hoist into a
/// shared metrics type so drawing and hit-testing can't drift.
/// </remarks>
internal sealed class OverlayRenderer : IDisposable
{
    private static readonly SKColor PlayheadColour = new(230, 60, 60);

    private readonly SKPaint _playhead = new()
    {
        Color = PlayheadColour,
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    private readonly SKPaint _clipDim = new() { Color = new SKColor(0, 0, 0, 120), Style = SKPaintStyle.Fill };

    public void Draw(SKCanvas canvas, float w, float h, TimelineOverlay overlay)
    {
        canvas.Save();

        canvas.ClipRect(new SKRect(overlay.BandLabelWidth, 0, w, h));

        if (overlay.SelectionActive)
        {
            if (overlay.SelectionLoX > overlay.BandLabelWidth)
            {
                canvas.DrawRect(overlay.BandLabelWidth, 0, overlay.SelectionLoX - overlay.BandLabelWidth, h, _clipDim);
            }

            if (overlay.SelectionHiX < w)
            {
                canvas.DrawRect(overlay.SelectionHiX, 0, w - overlay.SelectionHiX, h, _clipDim);
            }
        }

        canvas.DrawLine(overlay.PlayheadX, 0, overlay.PlayheadX, h, _playhead);

        canvas.Restore();
    }

    public void Dispose()
    {
        _playhead.Dispose();
        _clipDim.Dispose();
    }
}
