using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using InternalsViewer.UI.App.Controls.Query.Timeline;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const float RulerHeight = 18f;

    private const float PlayheadStripHeight = 16f;

    private const float MemoryBarHeight = 30f;

    private const float LaneHeaderHeight = 18f;

    private const float MaximumRowHeight = 16f;

    private const float FocusedMaximumRowHeight = 32f;

    private const float MinimumRowHeight = 2f;

    private const float MinimumLabelRowHeight = 11f;

    private const float GappedRowHeight = 4f;

    private const float LaneGap = 4f;

    private const float ChevronSize = 7f;

    private const float MinimumLabelWidth = 30f;

    internal const float LabelPadding = 3f;

    private const float TickSpacing = 110f;

    internal const float NarrowSpan = 1f;

    private const float TickHighlightOutset = 1f;

    private const byte RunAlpha = 190;

    private float _rowHeight = MaximumRowHeight;

    private SKPicture? _staticLayer;

    private LayerKey _staticLayerKey;

    private float BarHeight => _rowHeight >= GappedRowHeight ? _rowHeight - 1 : _rowHeight;

    private double EdgeTolerance => (_viewEnd - _viewStart) / Math.Max(_overlay.ActualWidth, 1);

    private bool ShowsStart => _viewStart <= FullStart + EdgeTolerance;

    private bool ShowsEnd => _viewEnd >= FullEnd - EdgeTolerance;

    private double PixelsPerUnit(int width) => width / Math.Max(_viewEnd - _viewStart, double.Epsilon);

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        ReportCallStack();

        ReportPlayhead();

        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        var scale = (float)_canvas.Dpi;

        canvas.Scale(scale);

        var width = (int)(e.Info.Width / scale);

        var height = (int)(e.Info.Height / scale);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var key = new LayerKey(width, height, _viewStart, _viewEnd, _scrollY, _version);

        if (_staticLayer is null || !key.Equals(_staticLayerKey))
        {
            _staticLayer?.Dispose();

            _staticLayer = RecordStaticLayer(width, height);

            _staticLayerKey = key;
        }

        canvas.DrawPicture(_staticLayer);

        if (ShowMemory)
        {
            var landscapeKey = new LandscapeKey(key, _extrusionAngle, _extrusionLength);

            if (_landscapeLayer is null || !landscapeKey.Equals(_landscapeLayerKey))
            {
                _landscapeLayer?.Dispose();

                _landscapeLayer = RecordLandscape(width, height);

                _landscapeLayerKey = landscapeKey;
            }

            canvas.DrawPicture(_landscapeLayer);
        }

        if (_visible is not null && ShowsMemory)
        {
            _memory.Draw(canvas, width, PixelsPerUnit(width));
        }

        DrawOverlay(canvas, width, height);
    }

    private SKPicture RecordStaticLayer(int width, int height)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        if (_visible is { } visible)
        {
            var scale = PixelsPerUnit(width);

            DrawPanes(canvas, width, scale);

            canvas.Save();

            var lanesBottom = height - (int)BandHeight;

            canvas.ClipRect(new SKRect(0, ContentTop, width, lanesBottom));

            DrawCallHighlights(canvas, visible, lanesBottom, width, scale);

            DrawLanes(canvas, visible, lanesBottom, width, scale);

            canvas.Restore();

            if (ShowsMemoryBand)
            {
                DrawMemoryBand(canvas, visible, width, height, scale);
            }

            canvas.Save();

            canvas.Translate(0, RulerTop);

            DrawRuler(canvas, width, scale);

            canvas.Restore();
        }

        return recorder.EndRecording();
    }

    private void DrawLanes(SKCanvas canvas, TraceTimeline timeline, int height, int width, double scale)
    {
        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var laneTop = ContentTop + _laneTops[lane] - (float)_scrollY;

            if (laneTop > height)
            {
                break;
            }

            if (laneTop + LaneHeaderHeight >= ContentTop)
            {
                DrawLaneHeader(canvas, timeline.Threads[lane], laneTop, width, timeline.Threads.Count > 1);
            }
        }

        foreach (var visible in VisibleRows(timeline, height))
        {
            DrawRow(canvas, visible, scale, width, RaisedOf(visible.Thread, visible.Lane, visible.Depth));
        }
    }

    private IEnumerable<VisibleRow> VisibleRows(TraceTimeline timeline, float bottom, float rise = 0)
    {
        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var thread = timeline.Threads[lane];

            var rowsTop = ContentTop + _laneTops[lane] - (float)_scrollY + LaneHeaderHeight;

            for (var depth = 0; depth < DepthOf(thread) && depth < thread.Rows.Count; depth++)
            {
                var top = rowsTop + depth * _rowHeight;

                if (top - rise > bottom)
                {
                    yield break;
                }

                if (top + _rowHeight >= ContentTop)
                {
                    yield return new VisibleRow(lane, thread, depth, thread.Rows[depth], top);
                }
            }
        }
    }

    private void DrawLaneHeader(SKCanvas canvas, TraceTimelineThread thread, float top, int width, bool collapsible)
    {
        _paints.Fill.Color = _paints.LaneHeader;

        canvas.DrawRect(0, top, width, LaneHeaderHeight, _paints.Fill);

        var left = LabelPadding * 2;

        if (collapsible)
        {
            DrawChevron(canvas, left + ChevronSize / 2, top + LaneHeaderHeight / 2, IsExpanded(thread));

            left += ChevronSize + LabelPadding * 2;
        }

        canvas.DrawText($"Thread {thread.ThreadId}",
                        left,
                        Baseline(top, LaneHeaderHeight),
                        SKTextAlign.Left,
                        _paints.HeaderFont,
                        _paints.Label);
    }

    private void DrawChevron(SKCanvas canvas, float centreX, float centreY, bool expanded)
    {
        var half = ChevronSize / 2;

        if (expanded)
        {
            _pathBuilder.MoveTo(centreX - half, centreY - half / 2);
            _pathBuilder.LineTo(centreX + half, centreY - half / 2);
            _pathBuilder.LineTo(centreX, centreY + half / 2);
        }
        else
        {
            _pathBuilder.MoveTo(centreX - half / 2, centreY - half);
            _pathBuilder.LineTo(centreX + half / 2, centreY);
            _pathBuilder.LineTo(centreX - half / 2, centreY + half);
        }

        _pathBuilder.Close();

        using var path = _pathBuilder.Detach();

        canvas.DrawPath(path, _paints.Label);
    }

    private Func<int, bool>? RaisedOf(TraceTimelineThread thread, int lane, int depth)
    {
        if (!ShowMemory || _visible is not { } timeline)
        {
            return null;
        }

        if (MemoryMode == TraceTimelineMemoryMode.InUse)
        {
            var row = thread.Rows[depth];

            return index =>
            {
                return Memory.AllocatedDuring(thread.ThreadId, row.GetFrom(index).Position, row.GetTo(index).Position).Count > 0;
            };
        }

        return _raisedRows.TryGetValue((lane, depth), out var raised) ? index => Array.BinarySearch(raised.Indexes, index) >= 0 : null;
    }

    private void DrawRow(SKCanvas canvas, VisibleRow visible, double scale, int width, Func<int, bool>? raised)
    {
        var row = visible.Row;

        var top = visible.Top;

        var runStart = -1f;

        var runEnd = -1f;

        var runColour = SKColors.Transparent;

        var index = row.GetFirstEndingAfter(_axis, _viewStart, 0);

        while (index < row.Count && row.GetStart(_axis, index) <= _viewEnd)
        {
            var left = (float)((row.GetStart(_axis, index) - _viewStart) * scale);

            var right = (float)((row.GetEnd(_axis, index) - _viewStart) * scale);

            if (right - left >= NarrowSpan)
            {
                DrawRun(canvas, runStart, runEnd, runColour, top);

                runStart = -1f;

                var labelled = raised is null || !raised(index);

                DrawSpan(canvas, row.GetCallNodeIndex(index), left, right, top, width, labelled);

                index++;

                continue;
            }

            var colour = ColourOf(row.GetCallNodeIndex(index));

            var isContiguous = runStart >= 0 && left <= runEnd + NarrowSpan;

            if (!isContiguous || colour != runColour)
            {
                DrawRun(canvas, runStart, runEnd, runColour, top);

                runStart = isContiguous ? Math.Max(left, runEnd) : left;

                runColour = colour;
            }

            var boundary = MathF.Floor(Math.Max(right, left)) + 1f;

            runEnd = boundary;

            index = row.GetFirstEndingAfter(_axis, _viewStart + boundary / scale, index + 1);
        }

        DrawRun(canvas, runStart, runEnd, runColour, top);
    }

    private void DrawRun(SKCanvas canvas, float start, float end, SKColor colour, float top)
    {
        if (start < 0)
        {
            return;
        }

        _paints.Fill.Color = colour.WithAlpha((byte)(colour.Alpha * RunAlpha / 255));

        canvas.DrawRect(start, top, Math.Max(end - start, NarrowSpan), BarHeight, _paints.Fill);
    }

    private void DrawSpan(SKCanvas canvas, int callNodeIndex, float left, float right, float top, int width, bool labelled)
    {
        var colour = ColourOf(callNodeIndex);

        var visibleLeft = Math.Max(left, -1f);

        var visibleRight = Math.Min(right, width + 1f);

        var rect = new SKRect(visibleLeft, top, Math.Max(visibleRight - 1f, visibleLeft + NarrowSpan), top + BarHeight);

        _paints.Fill.Color = colour;

        canvas.DrawRect(rect, _paints.Fill);

        if (!labelled || rect.Width < MinimumLabelWidth || _rowHeight < MinimumLabelRowHeight)
        {
            return;
        }

        _paints.Text.Color = (TimelineColours.IsLight(colour) ? SKColors.Black : SKColors.White).WithAlpha(colour.Alpha);

        canvas.Save();

        canvas.ClipRect(rect);

        canvas.DrawText(LabelOf(callNodeIndex),
                        Math.Max(rect.Left, 0) + LabelPadding,
                        Baseline(top, BarHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Text);

        canvas.Restore();
    }

    private void DrawRuler(SKCanvas canvas, int width, double scale)
    {
        var step = TickStep(TickSpacing / scale);

        var origin = OriginOf(_axis);

        var first = Math.Ceiling((Math.Max(_viewStart, FullStart) - origin) / step) * step + origin;

        for (var value = first; value <= Math.Min(_viewEnd, FullEnd); value += step)
        {
            var x = (float)((value - _viewStart) * scale);

            canvas.DrawLine(x, RulerHeight - 4, x, RulerHeight, _paints.Tick);

            var label = FormatAxis(value - origin, step);

            if (x + 3 + _paints.Font.MeasureText(label) < width)
            {
                canvas.DrawText(label, x + 4, Baseline(0, RulerHeight - 4), SKTextAlign.Left, _paints.Font, _paints.Label);
            }
        }

        canvas.DrawLine(0, RulerHeight, width, RulerHeight, _paints.Tick);
    }

    private void DrawOverlay(SKCanvas canvas, int width, int height)
    {
        var hover = _hover;

        var isTickHover = _operators.Hover.Tick?.Tick is not null;

        var hoverPaint = isTickHover ? _paints.TickHighlight : _paints.Hover;

        canvas.Save();

        canvas.ClipRect(new SKRect(0, ContentTop, width, height - BandHeight));

        DrawSelectedCalls(canvas, width, height);

        DrawHighlight(canvas, hover, width, hoverPaint, isTickHover ? TickHighlightOutset : 0);

        DrawHighlight(canvas, _selected, width, _paints.Selection);

        canvas.Restore();

        DrawPaneOverlays(canvas, width);

        DrawSearchMatches(canvas, width, height);

        if (ShowMemory)
        {
            canvas.Save();

            canvas.ClipRect(new SKRect(0, 0, width, height - BandHeight));

            DrawRaisedHighlight(canvas, hover, hoverPaint);

            DrawRaisedHighlight(canvas, _selected, _paints.Selection);

            canvas.Restore();
        }

        DrawPlayhead(canvas, width, height);

        DrawHover(canvas);

        DrawMark(canvas, width, height);

        if (_isSelecting)
        {
            var rect = SKRect.Create((float)Math.Min(_pressPoint.X, _dragPoint.X),
                                     (float)Math.Min(_pressPoint.Y, _dragPoint.Y),
                                     (float)Math.Abs(_dragPoint.X - _pressPoint.X),
                                     (float)Math.Abs(_dragPoint.Y - _pressPoint.Y));

            canvas.DrawRect(rect, _paints.DragFill);

            canvas.DrawRect(rect, _paints.DragStroke);
        }
    }

    private void DrawHover(SKCanvas canvas)
    {
        if (!ShowMemory || _hoverBlock is not { } hovered)
        {
            return;
        }

        if (MemoryMode == TraceTimelineMemoryMode.InUse)
        {
            canvas.DrawRect(FrontOf(hovered), _paints.Hover);
        }

        if (hovered.Bytes > 0)
        {
            DrawBlockLabel(canvas, hovered);
        }
    }

    private void DrawHighlight(SKCanvas canvas, FlameHit? hit, int width, SKPaint paint, float outset = 0)
    {
        if (hit is not { } highlighted || (ShowMemory && SpikeOf(highlighted) is not null))
        {
            return;
        }

        if (SpanRect(highlighted, width) is { } rect)
        {
            canvas.DrawRect(SKRect.Inflate(rect, outset, outset), paint);
        }
    }

    private SKRect? SpanRect(FlameHit hit, int width)
    {
        if (_visible is not { } timeline || hit.Lane >= timeline.Threads.Count)
        {
            return null;
        }

        var thread = timeline.Threads[hit.Lane];

        if (hit.Depth >= DepthOf(thread))
        {
            return null;
        }

        var row = thread.Rows[hit.Depth];

        if (hit.Index >= row.Count)
        {
            return null;
        }

        var scale = PixelsPerUnit(width);

        var left = (float)((row.GetStart(_axis, hit.Index) - _viewStart) * scale);

        var right = (float)((row.GetEnd(_axis, hit.Index) - _viewStart) * scale);

        var top = ContentTop + _laneTops[hit.Lane] - (float)_scrollY + LaneHeaderHeight + hit.Depth * _rowHeight;

        return new SKRect(Math.Max(left, -2f), top, Math.Min(Math.Max(right, left + NarrowSpan), width + 2f), top + BarHeight);
    }

    private float Baseline(float top, float height)
    {
        var metrics = _paints.Font.Metrics;

        return top + (height - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;
    }

    private SKColor ColourOf(int callNodeIndex)
    {
        if (_colours.TryGetValue(callNodeIndex, out var colour))
        {
            return colour;
        }

        colour = GetCategoryColour(callNodeIndex);

        if (IsDimmed(callNodeIndex))
        {
            colour = Dimmed(colour);
        }

        _colours[callNodeIndex] = colour;

        return colour;
    }

    private SKColor GetPaneColour(int callNodeIndex) => GetCategoryColour(callNodeIndex);

    private SKColor GetCategoryColour(int callNodeIndex)
        => _timeline?.NodeOf(callNodeIndex)?.CategoryColour is { } hex ? Parse(hex) : _paints.Unknown;

    private SKColor Parse(string hex)
    {
        if (!_parsedColours.TryGetValue(hex, out var colour))
        {
            colour = SKColor.TryParse(hex, out var parsed) ? parsed : _paints.Unknown;

            _parsedColours[hex] = colour;
        }

        return colour;
    }

    private string LabelOf(int callNodeIndex)
    {
        if (!_labels.TryGetValue(callNodeIndex, out var label))
        {
            label = _timeline?.NodeOf(callNodeIndex)?.Symbol is { Length: > 0 } symbol ? symbol : "Unknown";

            _labels[callNodeIndex] = label;
        }

        return label;
    }

    private static double TickStep(double minimum)
    {
        if (minimum <= 0 || double.IsNaN(minimum) || double.IsInfinity(minimum))
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(minimum)));

        foreach (var factor in (ReadOnlySpan<double>)[1, 2, 5, 10])
        {
            if (magnitude * factor >= minimum)
            {
                return magnitude * factor;
            }
        }

        return magnitude * 10;
    }

    private string FormatAxis(double value, double step)
    {
        if (_axis == TraceTimelineAxis.Position)
        {
            return step >= 1 ? $"{value:N0}" : $"{value:N3}";
        }

        return value switch
        {
            >= 1_000_000_000 => $"{value / 1_000_000_000:0.##}B",
            >= 1_000_000 => $"{value / 1_000_000:0.##}M",
            >= 1_000 => $"{value / 1_000:0.##}K",
            _ => $"{value:N0}"
        };
    }

    private readonly record struct VisibleRow(int Lane, TraceTimelineThread Thread, int Depth, TraceTimelineRow Row, float Top);

    private readonly record struct LayerKey(int Width,
                                            int Height,
                                            double ViewStart,
                                            double ViewEnd,
                                            double ScrollY,
                                            int Version);

    private sealed record RaisedRow(int[] Indexes, ulong[] Bytes);
}
