using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const float MinimapWidth = 360f;

    private const float MinimapHeight = 144f;

    private const float MinimapMargin = 8f;

    private readonly TraceTimelineMinimap _minimap = new()
    {
        Width = MinimapWidth,
        Height = MinimapHeight,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom,
        Visibility = Visibility.Collapsed
    };

    private readonly Dictionary<(CallTreeNode Node, int CallIndex, TraceTimelineAxis Axis), MarkedCall?> _markedCalls = [];

    private TracePoint[] _markedPoints = [];

    private TracePoint[] _pinnedPoints = [];

    private (TracePoint[] Points, TraceTimelineAxis Axis, double[] Values)? _markedValues;

    private (double Current, double[] Marked)? _minimapMarkers;

    public int PinnedPositionCount => _pinnedPoints.Length;

    private TraceTimeline? Overview => FullTimeline ?? _timeline;

    public void MarkPositions(TracePoint[]? points)
    {
        _markedPoints = points ?? [];

        _canvas.Invalidate();
    }

    public void PinPositions(TracePoint[] points)
    {
        _pinnedPoints = points;

        _canvas.Invalidate();
    }

    public void MovePlayheadTo(TracePoint point)
    {
        RememberLocation();

        StopPlay();

        PlacePlayhead(TraceScale.GetValue(point, _axis));
    }

    public void ZoomToCall(CallTreeNode node, int callIndex)
    {
        if (RevealSpan(node, callIndex) is not { } span)
        {
            return;
        }

        if (_visible is { } timeline && FindCall(timeline, node, callIndex) is { } hit)
        {
            ZoomToAndSelect(hit);

            return;
        }

        ZoomTo(span);
    }

    private TraceSpan? RevealSpan(CallTreeNode node, int callIndex)
    {
        if (GetCall(node, callIndex) is not { } found)
        {
            return null;
        }

        if (_root is not null && GetMarkedTop(found) is null)
        {
            SetRoot(null);
        }

        return found.Span;
    }

    private void ClearMark()
    {
        _markedPoints = [];

        _markedValues = null;

        _markedCalls.Clear();
    }

    private void ClearPins()
    {
        if (_pinnedPoints.Length == 0)
        {
            return;
        }

        _pinnedPoints = [];

        _canvas.Invalidate();
    }

    private void DrawMark(SKCanvas canvas, int width, int height)
    {
        var points = _markedPoints.Length > 0 ? _markedPoints : _pinnedPoints;

        if (points.Length == 0)
        {
            HideMinimap();

            return;
        }

        var values = GetMarkedValues(points);

        var pixelsPerUnit = PixelsPerUnit(width);

        var bottom = height - BandHeight;

        var paint = values.Length > 1 ? _paints.Marks : _paints.Mark;

        var isOutside = false;

        var lastPixel = float.NaN;

        foreach (var value in values)
        {
            var x = (float)((value - _viewStart) * pixelsPerUnit);

            if (x < 0 || x > width)
            {
                isOutside = true;

                continue;
            }

            var pixel = MathF.Round(x);

            if (pixel.Equals(lastPixel))
            {
                continue;
            }

            lastPixel = pixel;

            canvas.DrawLine(x, ContentTop, x, bottom, paint);
        }

        if (isOutside)
        {
            ShowMinimap(values, width, height);
        }
        else
        {
            HideMinimap();
        }
    }

    private double[] GetMarkedValues(TracePoint[] points)
    {
        if (_markedValues is { } cached && ReferenceEquals(cached.Points, points) && cached.Axis == _axis)
        {
            return cached.Values;
        }

        var values = new double[points.Length];

        for (var index = 0; index < points.Length; index++)
        {
            values[index] = TraceScale.GetValue(points[index], _axis);
        }

        Array.Sort(values);

        _markedValues = (points, _axis, values);

        return values;
    }

    private float? GetMarkedTop(MarkedCall marked)
    {
        if (_visible is not { } visible)
        {
            return null;
        }

        for (var lane = 0; lane < visible.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var thread = visible.Threads[lane];

            if (thread.ThreadId != marked.ThreadId)
            {
                continue;
            }

            for (var depth = 0; depth < thread.Rows.Count; depth++)
            {
                var row = thread.Rows[depth];

                var index = row.GetIndex(marked.Span.From.Position);

                if (index < 0 || row.GetCallIndex(index) != marked.Span.CallIndex || row.GetCallNodeIndex(index) != marked.Span.CallNodeIndex)
                {
                    continue;
                }

                return ContentTop + _laneTops[lane] - (float)_scrollY + LaneHeaderHeight + Math.Min(depth, DepthOf(thread)) * _rowHeight;
            }

            return null;
        }

        return null;
    }

    private void ShowMinimap(ReadOnlySpan<double> marked, int width, int height)
    {
        var fits = width >= MinimapWidth + MinimapMargin * 2 && height - BandHeight - ContentTop >= MinimapHeight + MinimapMargin * 2;

        if (!fits)
        {
            HideMinimap();

            return;
        }

        _minimap.Timeline = Overview;
        _minimap.Axis = _axis;
        _minimap.WindowStart = _viewStart;
        _minimap.WindowEnd = _viewEnd;
        _minimap.Crop = ReferenceEquals(Overview, _timeline) ? null : _timeline;
        _minimap.Scope = _rooted ?? _minimap.Crop;
        _minimap.Margin = new Thickness(0, 0, MinimapMargin, MinimapMargin + BandHeight);

        var current = _playhead ?? double.NaN;

        if (_minimapMarkers is not { } shown || !shown.Current.Equals(current) || !shown.Marked.AsSpan().SequenceEqual(marked))
        {
            var markers = new List<MinimapMarker>(marked.Length + 1);

            if (!double.IsNaN(current))
            {
                markers.Add(GetMinimapMarker(current, _paints.Playhead));
            }

            var paint = marked.Length > 1 ? _paints.Marks : _paints.Mark;

            foreach (var value in marked)
            {
                markers.Add(GetMinimapMarker(value, paint));
            }

            _minimap.Markers = markers;

            _minimapMarkers = (current, marked.ToArray());
        }

        _minimap.Visibility = Visibility.Visible;
    }

    private void HideMinimap() => _minimap.Visibility = Visibility.Collapsed;

    private static MinimapMarker GetMinimapMarker(double position, SKPaint paint)
    {
        var colour = Color.FromArgb(paint.Color.Alpha, paint.Color.Red, paint.Color.Green, paint.Color.Blue);

        return new MinimapMarker(position, colour, paint.StrokeWidth);
    }

    private void ReleaseMinimap()
    {
        _minimap.Timeline = null;
        _minimap.Crop = null;
        _minimap.Scope = null;
        _minimap.Markers = null;

        _minimapMarkers = null;
    }

    private MarkedCall? GetCall(CallTreeNode node, int callIndex)
    {
        if (callIndex < 0 || Overview is not { } timeline)
        {
            return null;
        }

        var key = (node, callIndex, _axis);

        if (!_markedCalls.TryGetValue(key, out var found))
        {
            found = FindCall(timeline, node, callIndex) is { } hit ? MarkedCallAt(timeline, hit) : null;

            _markedCalls[key] = found;
        }

        return found;
    }

    private MarkedCall MarkedCallAt(TraceTimeline timeline, FlameHit hit)
    {
        var thread = timeline.Threads[hit.Lane];

        return new MarkedCall(thread.Rows[hit.Depth].GetSpan(hit.Index), thread.ThreadId);
    }

    private readonly record struct MarkedCall(TraceSpan Span, uint ThreadId);
}
