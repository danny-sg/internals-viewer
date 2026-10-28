using System;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack;
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

    private static readonly Color CurrentColour = Color.FromArgb(255, 230, 60, 60);

    private static readonly Color MarkedColour = Color.FromArgb(255, 0, 0, 0);

    private readonly TraceTimelineMinimap _minimap = new()
    {
        Width = MinimapWidth,
        Height = MinimapHeight,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom,
        Visibility = Visibility.Collapsed
    };

    private readonly Dictionary<(CallStackNode Node, int CallIndex, TraceTimelineAxis Axis), MarkedCall?> _markedCalls = [];

    private (CallStackNode Node, int CallIndex)? _marked;

    private TracePoint[] _markedPoints = [];

    private TracePoint[] _pinnedPoints = [];

    private (TracePoint[] Points, TraceTimelineAxis Axis, double[] Values)? _markedValues;

    private (double Current, double[] Marked, int Lane, int Depth)? _minimapMarkers;

    public int PinnedPositionCount => _pinnedPoints.Length;

    private TraceTimeline? Overview => FullTimeline ?? _timeline;

    public void MarkCall(CallStackNode? node, int callIndex)
    {
        _marked = node is not null && callIndex >= 0 ? (node, callIndex) : null;

        _canvas.Invalidate();
    }

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

    public void MovePlayheadTo(CallStackNode node, int callIndex)
    {
        if (RevealCall(node, callIndex) is not { } start)
        {
            return;
        }

        StopPlay();

        PlacePlayhead(start);
    }

    public void MovePlayheadTo(TracePoint point)
    {
        StopPlay();

        PlacePlayhead(TraceScale.GetValue(point, _axis));
    }

    public void ZoomToCall(CallStackNode node, int callIndex)
    {
        if (RevealSpan(node, callIndex) is { } span)
        {
            ZoomTo(span);
        }
    }

    private double? RevealCall(CallStackNode node, int callIndex)
        => RevealSpan(node, callIndex) is { } span ? TraceScale.GetValue(span.From, _axis) : null;

    private TraceSpan? RevealSpan(CallStackNode node, int callIndex)
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
        _marked = null;

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
        if (GetMarkedCall() is { } marked)
        {
            DrawMarkedCall(canvas, marked, width, height);

            return;
        }

        var points = _markedPoints.Length > 0 ? _markedPoints : _pinnedPoints;

        if (points.Length == 0)
        {
            HideMinimap();

            return;
        }

        var values = GetMarkedValues(points);

        var pixelsPerUnit = PixelsPerUnit(width);

        var bottom = height - BandHeight;

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

            canvas.DrawLine(x, ContentTop, x, bottom, _paints.Mark);
        }

        if (isOutside)
        {
            ShowMinimap(values, -1, -1, width, height);
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

    private void DrawMarkedCall(SKCanvas canvas, MarkedCall marked, int width, int height)
    {
        var start = TraceScale.GetValue(marked.Span.From, _axis);

        var x = (float)((start - _viewStart) * PixelsPerUnit(width));

        if (x < 0 || x > width || GetMarkedTop(marked) is not { } rowTop)
        {
            ShowMinimap([start], marked.Lane, marked.Depth, width, height);

            return;
        }

        var bottom = height - BandHeight;

        canvas.DrawLine(x, Math.Max(ContentTop, Math.Min(rowTop, bottom)), x, bottom, _paints.Mark);

        HideMinimap();
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

    private void ShowMinimap(ReadOnlySpan<double> marked, int lane, int depth, int width, int height)
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

        if (_minimapMarkers is not { } shown
            || !shown.Current.Equals(current)
            || !shown.Marked.AsSpan().SequenceEqual(marked)
            || shown.Lane != lane
            || shown.Depth != depth)
        {
            var markers = new List<MinimapMarker>(marked.Length + 1);

            if (!double.IsNaN(current))
            {
                markers.Add(new MinimapMarker(current, CurrentColour));
            }

            foreach (var value in marked)
            {
                markers.Add(new MinimapMarker(value, MarkedColour, lane, depth));
            }

            _minimap.Markers = markers;

            _minimapMarkers = (current, marked.ToArray(), lane, depth);
        }

        _minimap.Visibility = Visibility.Visible;
    }

    private void HideMinimap() => _minimap.Visibility = Visibility.Collapsed;

    private void ReleaseMinimap()
    {
        _minimap.Timeline = null;
        _minimap.Crop = null;
        _minimap.Scope = null;
        _minimap.Markers = null;

        _minimapMarkers = null;
    }

    private MarkedCall? GetMarkedCall() => _marked is { } marked ? GetCall(marked.Node, marked.CallIndex) : null;

    private MarkedCall? GetCall(CallStackNode node, int callIndex)
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

        return new MarkedCall(thread.Rows[hit.Depth].GetSpan(hit.Index), thread.ThreadId, hit.Lane, hit.Depth);
    }

    private readonly record struct MarkedCall(TraceSpan Span, uint ThreadId, int Lane, int Depth);
}
