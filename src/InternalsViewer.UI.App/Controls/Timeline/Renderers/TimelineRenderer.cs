using System;
using InternalsViewer.Query.Events.Reads;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Draws the timeline's structural chrome: the alternating row backgrounds, each row's label, the separators between
/// rows, and the time ruler
/// </summary>
/// <remarks>
/// The static frame beneath the data lanes. Owns the row-background/separator/tick paints; the label font and paint are
/// shared (also used by the playhead badge). The row layout itself is computed by the control and passed in the frame.
/// </remarks>
internal sealed class TimelineRenderer(RenderResource resources) : IDisposable
{
    private const float RulerBandHeight = 18f;

    // The Read row shows three stacked labels only when it can fit them with at least this gap and vertical padding.
    private const float MinLabelGap = 1f;
    private const float VerticalLabelPad = 1f;

    // Roughly one ruler tick per this many pixels of drawable width.
    private const float PixelsPerTick = 80f;

    private readonly SKPaint _rowBackground = new() { Style = SKPaintStyle.Fill };
    private readonly SKPaint _separator = new() { Color = new SKColor(60, 60, 60), StrokeWidth = 1 };

    private readonly SKPaint _tick = new()
    {
        Color = new SKColor(110, 110, 110),
        StrokeWidth = 1,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    // The alternating row backgrounds, each row's label (the split Read row labels its two lanes), and the separators.
    public void DrawRows(SKCanvas canvas, TimelineFrame frame)
    {
        var rows = frame.Rows.Active;
        var w = frame.CanvasWidth;

        for (var r = 0; r < rows.Count; r++)
        {
            var y = frame.RowTops[r];
            var rowHeight = frame.RowHeights[r];

            _rowBackground.Color = r % 2 == 0 ? frame.LaneColour : frame.AlternateLaneColour;

            canvas.DrawRect(0, y, w, rowHeight, _rowBackground);

            // The split Read band labels its two lanes (Buffer / Disk) when tall enough; every other row (and a Read
            // row too short for three labels) keeps its single centred, left-aligned label.
            if (rows[r].EventType != typeof(ReadEventGroup) || !TryDrawReadRowLabels(canvas, y, rowHeight))
            {
                var blob = frame.Rows.LabelBlob(r);

                if (blob is not null)
                {
                    canvas.DrawText(blob, 2, y + rowHeight / 2 + resources.LabelFont.Size / 2, resources.LabelPaint);
                }
            }

            canvas.DrawLine(0, y + rowHeight, w, y + rowHeight, _separator);
        }
    }

    // The time ruler: a tick and time label at each "nice" interval across the visible window.
    public void DrawRuler(SKCanvas canvas, TimelineFrame frame)
    {
        var leftMs = frame.XToTime(frame.RowLabelWidth) - frame.MinTime;
        var rightMs = frame.XToTime(frame.CanvasWidth) - frame.MinTime;

        var rangeMs = rightMs - leftMs;

        if (rangeMs <= 0)
        {
            return;
        }

        var drawWidth = frame.CanvasWidth - frame.RowLabelWidth;

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

            canvas.DrawLine(x, RulerBandHeight - 4, x, RulerBandHeight, _tick);

            textBuffer.Clear();

            var length = TimelineFormat.FormatTimeIntoSpan(tickMs, textBuffer);

            using var blob = SKTextBlob.Create(textBuffer[..length], resources.LabelFont, SKPoint.Empty);

            if (blob is not null)
            {
                canvas.DrawText(blob, x + 2, RulerBandHeight - 6, resources.LabelPaint);
            }
        }
    }

    public void Dispose()
    {
        _rowBackground.Dispose();
        _separator.Dispose();
        _tick.Dispose();
    }

    // Draws the split Read row's three labels — "Buffer" (cached lane), "Disk" (physical lane), "Read" (centred), all
    // left-aligned like the single-label rows. Returns false when the row is too short to fit all three.
    private bool TryDrawReadRowLabels(SKCanvas canvas, float rowTop, float rowHeight)
    {
        var metrics = resources.LabelFont.Metrics;

        var textHeight = metrics.Descent - metrics.Ascent;

        if (rowHeight < textHeight * 3 + MinLabelGap * 2 + VerticalLabelPad * 2)
        {
            return false;
        }

        canvas.DrawText("Buffer", 4, rowTop + VerticalLabelPad - metrics.Ascent, SKTextAlign.Left,
                        resources.LabelFont, resources.LabelPaint);

        canvas.DrawText("Read", 2, rowTop + rowHeight / 2 - (metrics.Ascent + metrics.Descent) / 2,
                        SKTextAlign.Left, resources.LabelFont, resources.LabelPaint);

        canvas.DrawText("Disk", 4, rowTop + rowHeight - VerticalLabelPad - metrics.Descent, SKTextAlign.Left,
                        resources.LabelFont, resources.LabelPaint);

        return true;
    }
}
