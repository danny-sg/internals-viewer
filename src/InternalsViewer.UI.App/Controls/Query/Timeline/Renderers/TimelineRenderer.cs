using System;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Timeline.Renderers;

/// <summary>
/// Timeline chrome renderer
/// </summary>
/// <remarks>
/// The static frame beneath the data lanes
/// </remarks>
internal sealed class TimelineRenderer(RenderResource resources) : IDisposable
{
    // The Read row shows three stacked labels only when it can fit them with at least this gap and vertical padding
    private const float MinLabelGap = 1f;
    private const float VerticalLabelPad = 1f;

    private readonly SKPaint _bandBackground = new() { Style = SKPaintStyle.Fill };

    private readonly SKPaint _separator = new() { Color = new SKColor(60, 60, 60), StrokeWidth = 1 };

    private readonly SKPaint _trackDivider = new() { Color = new SKColor(64, 64, 64), StrokeWidth = 1 };

    private float SubBandLabelsHeight
    {
        get
        {
            var metrics = resources.LabelFont.Metrics;

            return (metrics.Descent - metrics.Ascent) * 3 + MinLabelGap * 2 + VerticalLabelPad * 2;
        }
    }

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

            DrawTrackDividers(canvas, frame, r, y, bandHeight);

            var hasSubBandLabels = bands[r].SubBandLabels is { } labels
                             && !frame.IsCollapsed(r)
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

    public void Dispose()
    {
        _bandBackground.Dispose();
        _separator.Dispose();
        _trackDivider.Dispose();
    }

    private void DrawTrackDividers(SKCanvas canvas, TimelineFrame frame, int band, float bandTop, float bandHeight)
    {
        var innerTop = bandTop + frame.BandPadding;

        var innerHeight = bandHeight - frame.BandPadding * 2;

        foreach (var divider in frame.Bands.Active[band].TrackDividers)
        {
            var (track, trackCount) = frame.TrackIn(band, divider.Track, divider.TrackCount);

            if (track == 0)
            {
                continue;
            }

            var y = MathF.Floor(innerTop + track * innerHeight / trackCount) - 1;

            canvas.DrawLine(frame.BandLabelWidth, y, frame.CanvasWidth, y, _trackDivider);
        }
    }

    private bool TryDrawSubBandLabels(SKCanvas canvas, float bandTop, float bandHeight, string top, string middle, string bottom)
    {
        var metrics = resources.LabelFont.Metrics;

        if (bandHeight < SubBandLabelsHeight)
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
