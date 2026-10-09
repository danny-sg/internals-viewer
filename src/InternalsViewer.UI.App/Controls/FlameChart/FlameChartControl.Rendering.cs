using System;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float RulerHeight = 20f;

    private const float LaneHeaderHeight = 18f;

    private const float MaximumRowHeight = 16f;

    private const float FocusedMaximumRowHeight = 32f;

    private const float MinimumRowHeight = 2f;

    private const float MinimumLabelRowHeight = 11f;

    private const float GappedRowHeight = 4f;

    private const float LaneGap = 4f;

    private const float ChevronSize = 7f;

    private const float MinimumLabelWidth = 30f;

    private const float LabelPadding = 3f;

    private const float TickSpacing = 110f;

    private const float NarrowSpan = 1f;

    private const byte RunAlpha = 190;

    private float _rowHeight = MaximumRowHeight;

    private SKPicture? _staticLayer;

    private LayerKey _staticLayerKey;

    private float BarHeight => _rowHeight >= GappedRowHeight ? _rowHeight - 1 : _rowHeight;

    private string AxisUnit => _axis == TimeTravelTimelineAxis.Position ? "Trace Position" : "Instructions Per Thread";

    private double PixelsPerUnit(int width) => width / Math.Max(_viewEnd - _viewStart, double.Epsilon);

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        var width = e.Info.Width;

        var height = e.Info.Height;

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

        DrawOverlay(canvas, width, height);
    }

    private SKPicture RecordStaticLayer(int width, int height)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        if (_visible is { } visible)
        {
            var scale = PixelsPerUnit(width);

            canvas.Save();

            canvas.ClipRect(new SKRect(0, RulerHeight, width, height));

            DrawLanes(canvas, visible, height, width, scale);

            canvas.Restore();

            DrawRuler(canvas, width, scale);
        }

        return recorder.EndRecording();
    }

    private void DrawLanes(SKCanvas canvas, TimeTravelTimeline timeline, int height, int width, double scale)
    {
        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var thread = timeline.Threads[lane];

            var depthCount = DepthOf(thread);

            var laneTop = RulerHeight + _laneTops[lane] - (float)_scrollY;

            if (laneTop + LaneHeaderHeight + depthCount * _rowHeight + LaneGap < RulerHeight)
            {
                continue;
            }

            if (laneTop > height)
            {
                break;
            }

            DrawLaneHeader(canvas, thread, laneTop, width, timeline.Threads.Count > 1);

            for (var depth = 0; depth < depthCount && depth < thread.Rows.Count; depth++)
            {
                var top = laneTop + LaneHeaderHeight + depth * _rowHeight;

                if (top + _rowHeight < RulerHeight)
                {
                    continue;
                }

                if (top > height)
                {
                    break;
                }

                DrawRow(canvas, thread.Rows[depth], top, scale, width);
            }
        }
    }

    private void DrawLaneHeader(SKCanvas canvas, TimeTravelTimelineThread thread, float top, int width, bool collapsible)
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
                        _paints.Font,
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

    private void DrawRow(SKCanvas canvas, TimeTravelTimelineRow row, float top, double scale, int width)
    {
        var starts = row.Starts(_axis);

        var ends = row.Ends(_axis);

        var runStart = -1f;

        var runEnd = -1f;

        var runColour = SKColors.Transparent;

        var index = row.FirstEndingAfter(_axis, _viewStart, 0);

        while (index < row.Count && starts[index] <= _viewEnd)
        {
            var left = (float)((starts[index] - _viewStart) * scale);

            var right = (float)((ends[index] - _viewStart) * scale);

            if (right - left >= NarrowSpan)
            {
                DrawRun(canvas, runStart, runEnd, runColour, top);

                runStart = -1f;

                DrawSpan(canvas, row.NodeAt(index), left, right, top, width);

                index++;

                continue;
            }

            if (runStart < 0 || left > runEnd + NarrowSpan)
            {
                DrawRun(canvas, runStart, runEnd, runColour, top);

                runStart = left;

                runColour = ColourOf(row.NodeAt(index));
            }

            var boundary = MathF.Floor(Math.Max(right, left)) + 1f;

            runEnd = boundary;

            index = row.FirstEndingAfter(_axis, _viewStart + boundary / scale, index + 1);
        }

        DrawRun(canvas, runStart, runEnd, runColour, top);
    }

    private void DrawRun(SKCanvas canvas, float start, float end, SKColor colour, float top)
    {
        if (start < 0)
        {
            return;
        }

        _paints.Fill.Color = colour.WithAlpha(RunAlpha);

        canvas.DrawRect(start, top, Math.Max(end - start, NarrowSpan), BarHeight, _paints.Fill);
    }

    private void DrawSpan(SKCanvas canvas, int node, float left, float right, float top, int width)
    {
        var colour = ColourOf(node);

        var visibleLeft = Math.Max(left, -1f);

        var visibleRight = Math.Min(right, width + 1f);

        var rect = new SKRect(visibleLeft, top, Math.Max(visibleRight - 1f, visibleLeft + NarrowSpan), top + BarHeight);

        _paints.Fill.Color = colour;

        canvas.DrawRect(rect, _paints.Fill);

        if (rect.Width < MinimumLabelWidth || _rowHeight < MinimumLabelRowHeight)
        {
            return;
        }

        _paints.Text.Color = IsLight(colour) ? SKColors.Black : SKColors.White;

        canvas.Save();

        canvas.ClipRect(rect);

        canvas.DrawText(LabelOf(node),
                        Math.Max(rect.Left, 0) + LabelPadding,
                        Baseline(top, BarHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Text);

        canvas.Restore();
    }

    private void DrawRuler(SKCanvas canvas, int width, double scale)
    {
        var unitLeft = width - _paints.Font.MeasureText(AxisUnit) - LabelPadding * 4;

        var step = TickStep(TickSpacing / scale);

        var first = Math.Ceiling(_viewStart / step) * step;

        for (var value = first; value <= _viewEnd; value += step)
        {
            var x = (float)((value - _viewStart) * scale);

            canvas.DrawLine(x, RulerHeight - 6, x, RulerHeight, _paints.Tick);

            var label = FormatAxis(value, step);

            if (x + 3 + _paints.Font.MeasureText(label) < unitLeft)
            {
                canvas.DrawText(label, x + 3, Baseline(0, RulerHeight - 4), SKTextAlign.Left, _paints.Font, _paints.Label);
            }
        }

        canvas.DrawLine(0, RulerHeight - 0.5f, width, RulerHeight - 0.5f, _paints.Tick);

        canvas.DrawText(AxisUnit, width - LabelPadding * 2, Baseline(0, RulerHeight - 4), SKTextAlign.Right, _paints.Font, _paints.Label);
    }

    private void DrawOverlay(SKCanvas canvas, int width, int height)
    {
        canvas.Save();

        canvas.ClipRect(new SKRect(0, RulerHeight, width, height));

        if (_hover is { } hover && SpanRect(hover, width) is { } hoverRect)
        {
            canvas.DrawRect(hoverRect, _paints.Hover);
        }

        if (_selected is { } selected && SpanRect(selected, width) is { } selectedRect)
        {
            canvas.DrawRect(selectedRect, _paints.Selection);
        }

        canvas.Restore();

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

        var left = (float)((row.Starts(_axis)[hit.Index] - _viewStart) * scale);

        var right = (float)((row.Ends(_axis)[hit.Index] - _viewStart) * scale);

        var top = RulerHeight + _laneTops[hit.Lane] - (float)_scrollY + LaneHeaderHeight + hit.Depth * _rowHeight;

        return new SKRect(Math.Max(left, -2f), top, Math.Min(Math.Max(right, left + NarrowSpan), width + 2f), top + BarHeight);
    }

    private float Baseline(float top, float height)
    {
        var metrics = _paints.Font.Metrics;

        return top + (height - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;
    }

    private SKColor ColourOf(int node)
    {
        if (_colours.TryGetValue(node, out var colour))
        {
            return colour;
        }

        colour = _timeline?.NodeOf(node)?.CategoryColour is { } hex ? Parse(hex) : _paints.Unknown;

        _colours[node] = colour;

        return colour;
    }

    private SKColor Parse(string hex)
    {
        if (!_parsedColours.TryGetValue(hex, out var colour))
        {
            colour = SKColor.TryParse(hex, out var parsed) ? parsed : _paints.Unknown;

            _parsedColours[hex] = colour;
        }

        return colour;
    }

    private string LabelOf(int node)
    {
        if (!_labels.TryGetValue(node, out var label))
        {
            label = _timeline?.NodeOf(node)?.Symbol is { Length: > 0 } symbol ? symbol : "Unknown";

            _labels[node] = label;
        }

        return label;
    }

    private static bool IsLight(SKColor colour) => colour.Red * 0.299 + colour.Green * 0.587 + colour.Blue * 0.114 > 160;

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
        if (_axis == TimeTravelTimelineAxis.Position)
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

    private readonly record struct LayerKey(int Width,
                                            int Height,
                                            double ViewStart,
                                            double ViewEnd,
                                            double ScrollY,
                                            int Version);
}
