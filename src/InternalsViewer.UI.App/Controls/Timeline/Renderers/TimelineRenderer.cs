using System;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Timeline chrome renderer
/// </summary>
/// <remarks>
/// The static frame beneath the data lanes
/// </remarks>
internal sealed class TimelineRenderer(RenderResource resources) : IDisposable
{
    private const float RulerStripHeight = 18f;

    // The Read row shows three stacked labels only when it can fit them with at least this gap and vertical padding
    private const float MinLabelGap = 1f;
    private const float VerticalLabelPad = 1f;

    // Roughly one ruler tick per this many pixels of drawable width.
    private const float PixelsPerTick = 80f;

    private readonly SKPaint _bandBackground = new() { Style = SKPaintStyle.Fill };

    private readonly SKPaint _separator = new() { Color = new SKColor(60, 60, 60), StrokeWidth = 1 };

    private readonly SKPaint _trackDivider = new() { Color = new SKColor(64, 64, 64), StrokeWidth = 1 };

    private readonly SKPaint _tick = new()
    {
        Color = new SKColor(110, 110, 110),
        StrokeWidth = 1,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    /// <remarks>
    /// Draws alternating row backgrounds, row labels, and separators
    /// </remarks>
    public void DrawBands(SKCanvas canvas, TimelineFrame frame)
    {
        var bands = frame.Bands.Active;
        var w = frame.CanvasWidth;

        for (var r = 0; r < bands.Count; r++)
        {
            var y = frame.BandTops[r];
            var bandHeight = frame.BandHeights[r];

            _bandBackground.Color = r % 2 == 0 ? frame.BandColour : frame.AlternateBandColour;

            canvas.DrawRect(0, y, w, bandHeight, _bandBackground);

            DrawTrackDividers(canvas, frame, bands[r], y, bandHeight);

            var hasSubBandLabels = bands[r].SubBandLabels is { } labels
                             && TryDrawSubBandLabels(canvas, y, bandHeight, labels.Top, labels.Middle, labels.Bottom);

            if (!hasSubBandLabels)
            {
                var blob = frame.Bands.LabelBlob(r);

                if (blob is not null)
                {
                    canvas.DrawText(blob, 2, y + bandHeight / 2 + resources.LabelFont.Size / 2, resources.LabelPaint);
                }
            }

            canvas.DrawLine(0, y + bandHeight, w, y + bandHeight, _separator);
        }
    }

    public void DrawEmpty(SKCanvas canvas, TimelineFrame frame, float top, float height)
    {
        _bandBackground.Color = frame.BandColour;

        canvas.DrawRect(0, top, frame.CanvasWidth, height, _bandBackground);
    }

    /// <summary>
    /// Draws the time ruler
    /// </summary>
    /// <remarks>
    /// Draws a tick and time label at each "nice" interval across the visible window
    /// </remarks>>
    public void DrawRuler(SKCanvas canvas, TimelineFrame frame)
    {
        var leftMs = frame.XToTime(frame.BandLabelWidth) - frame.MinTime;

        var rightMs = frame.XToTime(frame.CanvasWidth) - frame.MinTime;

        var rangeMs = rightMs - leftMs;

        if (rangeMs <= 0)
        {
            return;
        }

        var drawWidth = frame.CanvasWidth - frame.BandLabelWidth;

        var targetTicks = Math.Max(2, drawWidth / PixelsPerTick);

        var interval = TimelineFormat.NiceInterval(rangeMs / targetTicks);

        if (interval <= 0)
        {
            return;
        }

        Span<char> textBuffer = stackalloc char[12];

        for (var tickMs = Math.Ceiling(leftMs / interval) * interval; tickMs <= rightMs; tickMs += interval)
        {
            var x = frame.TimeToX(frame.MinTime + tickMs);

            canvas.DrawLine(x, RulerStripHeight - 4, x, RulerStripHeight, _tick);

            textBuffer.Clear();

            var length = TimelineFormat.FormatTimeIntoSpan(tickMs, textBuffer);

            using var blob = SKTextBlob.Create(textBuffer[..length], resources.LabelFont, SKPoint.Empty);

            if (blob is not null)
            {
                canvas.DrawText(blob, x + 2, RulerStripHeight - 6, resources.LabelPaint);
            }
        }
    }

    public void Dispose()
    {
        _bandBackground.Dispose();
        _separator.Dispose();
        _trackDivider.Dispose();
        _tick.Dispose();
    }

    private void DrawTrackDividers(SKCanvas canvas, TimelineFrame frame, TimelineBand band, float bandTop, float bandHeight)
    {
        var innerTop = bandTop + frame.BandPadding;

        var innerHeight = bandHeight - frame.BandPadding * 2;

        foreach (var divider in band.TrackDividers)
        {
            var y = MathF.Floor(innerTop + divider.Track * innerHeight / divider.TrackCount) - 1;

            canvas.DrawLine(frame.BandLabelWidth, y, frame.CanvasWidth, y, _trackDivider);
        }
    }

    private bool TryDrawSubBandLabels(SKCanvas canvas, float bandTop, float bandHeight, string top, string middle, string bottom)
    {
        var metrics = resources.LabelFont.Metrics;

        var textHeight = metrics.Descent - metrics.Ascent;

        if (bandHeight < textHeight * 3 + MinLabelGap * 2 + VerticalLabelPad * 2)
        {
            return false;
        }

        canvas.DrawText(top, 4, bandTop + VerticalLabelPad - metrics.Ascent, SKTextAlign.Left,
                        resources.LabelFont, resources.LabelPaint);

        canvas.DrawText(middle, 2, bandTop + bandHeight / 2 - (metrics.Ascent + metrics.Descent) / 2,
                        SKTextAlign.Left, resources.LabelFont, resources.LabelPaint);

        canvas.DrawText(bottom, 4, bandTop + bandHeight - VerticalLabelPad - metrics.Descent, SKTextAlign.Left,
                        resources.LabelFont, resources.LabelPaint);

        return true;
    }
}
