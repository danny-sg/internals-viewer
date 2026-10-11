using System;
using System.Linq;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float AllocatedBandHeight = 36f;

    private const float InUseBandHeight = 56f;

    private const float MemoryBandHeight = AllocatedBandHeight + InUseBandHeight;

    private const float MemoryBandLabelHeight = 14f;

    private const float MemoryBucketWidth = 2f;

    private const float GrantLabelGap = 3f;

    private const float MarkerRadius = 3f;

    private ulong _bandPeak;

    private ulong _inUsePeak;

    private ulong _inUseScale;

    private void DrawMemoryBand(SKCanvas canvas, TimeTravelTimeline timeline, int width, int height, double scale)
    {
        var top = height - MemoryBandHeight;

        var inUseTop = height - InUseBandHeight;

        var buckets = Math.Max(1, (int)Math.Ceiling(width / MemoryBucketWidth));

        var allocated = new ulong[buckets];

        var inUse = new ulong[buckets];

        _bandPeak = 0;

        _inUsePeak = 0;

        for (var bucket = 0; bucket < buckets; bucket++)
        {
            var (start, end) = BucketRange(bucket, scale);

            allocated[bucket] = AllocatedDuring(timeline, start, end);

            inUse[bucket] = BandInUse(timeline, start, end);

            _bandPeak = Math.Max(_bandPeak, allocated[bucket]);

            _inUsePeak = Math.Max(_inUsePeak, inUse[bucket]);
        }

        var granted = (ulong)Math.Max(0, GrantedMemory);

        _inUseScale = Math.Max(_inUsePeak, granted);

        var legendRight = width - LabelPadding * 4;

        canvas.DrawText("Allocated", legendRight, Baseline(top, MemoryBandLabelHeight), SKTextAlign.Right, _paints.Font, _paints.Label);

        if (_inUsePeak > 0)
        {
            canvas.DrawText($"{InUseLabel(timeline)}, Peak {SizeFormat.Format((long)_inUsePeak)}",
                            legendRight,
                            Baseline(inUseTop, MemoryBandLabelHeight),
                            SKTextAlign.Right,
                            _paints.Font,
                            _paints.InUseText);
        }

        if (_bandPeak > 0)
        {
            DrawBandLine(canvas, allocated, _bandPeak, inUseTop - 1f, AllocatedBandHeight, _paints.AreaLine);
        }

        if (granted > 0)
        {
            DrawGrant(canvas, granted, width, height);
        }

        if (_inUsePeak > 0)
        {
            DrawBandLine(canvas, inUse, _inUseScale, height - 1f, InUseBandHeight, _paints.InUseLine);
        }
    }

    private void DrawGrant(SKCanvas canvas, ulong granted, int width, int height)
    {
        var top = BandY(granted, _inUseScale, height - 1f, InUseBandHeight);

        canvas.DrawRect(0, top, width, height - top, _paints.GrantFill);

        canvas.DrawLine(0, top, width, top, _paints.GrantEdge);

        canvas.DrawText($"Granted {SizeFormat.Format((long)granted)}",
                        LabelPadding * 2,
                        top - GrantLabelGap,
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Label);
    }

    private void DrawBandLine(SKCanvas canvas, ulong[] values, ulong peak, float bottom, float rowHeight, SKPaint paint)
    {
        _pathBuilder.MoveTo(0.5f * MemoryBucketWidth, BandY(values[0], peak, bottom, rowHeight));

        for (var bucket = 1; bucket < values.Length; bucket++)
        {
            _pathBuilder.LineTo((bucket + 0.5f) * MemoryBucketWidth, BandY(values[bucket], peak, bottom, rowHeight));
        }

        using var line = _pathBuilder.Detach();

        canvas.DrawPath(line, paint);
    }

    private void DrawBandMarker(SKCanvas canvas, TimeTravelTimeline timeline, float x, int width, int height, double scale)
    {
        if (!ShowsMemoryBand || x < 0 || x >= width)
        {
            return;
        }

        var (start, end) = BucketRange((int)(x / MemoryBucketWidth), scale);

        var inUseTop = height - InUseBandHeight;

        if (_bandPeak > 0)
        {
            var allocated = AllocatedDuring(timeline, start, end);

            DrawMarker(canvas,
                       x,
                       BandY(allocated, _bandPeak, inUseTop - 1f, AllocatedBandHeight),
                       $"Allocated {SizeFormat.Format((long)allocated)}",
                       _paints.AreaLine.Color,
                       height - MemoryBandHeight,
                       inUseTop,
                       width);
        }

        if (_inUsePeak > 0)
        {
            var inUse = BandInUse(timeline, start, end);

            DrawMarker(canvas,
                       x,
                       BandY(inUse, _inUseScale, height - 1f, InUseBandHeight),
                       $"{InUseLabel(timeline)} {SizeFormat.Format((long)inUse)}",
                       _paints.InUseLine.Color,
                       inUseTop,
                       height,
                       width);
        }
    }

    private void DrawMarker(SKCanvas canvas, float x, float y, string text, SKColor colour, float top, float bottom, int width)
    {
        _paints.Face.Color = colour;

        canvas.DrawCircle(x, y, MarkerRadius, _paints.Face);

        var textWidth = _paints.Font.MeasureText(text);

        var left = x + MarkerRadius + LabelPadding + textWidth + LabelPadding * 2 > width
            ? x - MarkerRadius - LabelPadding - textWidth - LabelPadding * 2
            : x + MarkerRadius + LabelPadding;

        var labelTop = Math.Clamp(y - MemoryBandLabelHeight / 2, top + 1, bottom - MemoryBandLabelHeight - 1);

        canvas.DrawRect(new SKRect(left, labelTop, left + textWidth + LabelPadding * 2, labelTop + MemoryBandLabelHeight),
                        _paints.LabelBackground);

        canvas.DrawText(text,
                        left + LabelPadding,
                        Baseline(labelTop, MemoryBandLabelHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Label);
    }

    private bool ShowsGrantUse(TimeTravelTimeline timeline) => GrantedMemory > 0 && timeline.HasWorkspace;

    private ulong BandInUse(TimeTravelTimeline timeline, double start, double end)
        => ShowsGrantUse(timeline) ? timeline.PeakWorkspaceDuring(start, end) : timeline.PeakInUseDuring(start, end);

    private string InUseLabel(TimeTravelTimeline timeline) => ShowsGrantUse(timeline) ? "Grant In Use" : "In Use";

    private static ulong AllocatedDuring(TimeTravelTimeline timeline, double start, double end)
        => timeline.Threads.Aggregate(0ul, (total, t) => total + timeline.AllocatedDuring(t.ThreadId, start, end).Bytes);

    private (double Start, double End) BucketRange(int bucket, double scale)
    {
        var start = _viewStart + bucket * MemoryBucketWidth / scale;

        return (start, start + MemoryBucketWidth / scale);
    }

    private static float BandY(ulong bytes, ulong peak, float bottom, float rowHeight)
    {
        var chartHeight = rowHeight - MemoryBandLabelHeight - 2f;

        return peak == 0 ? bottom : bottom - chartHeight * (float)((double)bytes / peak);
    }
}
