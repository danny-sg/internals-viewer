using System;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float MinimapWidth = 360f;

    private const float MinimapHeight = 144f;

    private const float MinimapMargin = 8f;

    private static readonly Color CurrentColour = Color.FromArgb(255, 230, 60, 60);

    private readonly FlameChartMinimap _minimap = new()
    {
        Width = MinimapWidth,
        Height = MinimapHeight,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom,
        Visibility = Visibility.Collapsed
    };

    private readonly Dictionary<(CallStackNode Node, int Call, TraceTimelineAxis Axis), MarkedCall?> _markedCalls = [];

    private (CallStackNode Node, int Call)? _marked;

    private (double Current, MarkedCall Marked, SKColor Colour)? _minimapMarkers;

    private TraceTimeline? Overview => FullTimeline ?? _timeline;

    public void MarkCall(CallStackNode? node, int call)
    {
        _marked = node is not null && call >= 0 ? (node, call) : null;

        _canvas.Invalidate();
    }

    public void MovePlayheadTo(CallStackNode node, int call)
    {
        if (GetCall(node, call) is not { } found)
        {
            return;
        }

        if (_root is not null && GetMarkedTop(found) is null)
        {
            SetRoot(null);
        }

        StopPlay();

        StopTransition();

        PlacePlayhead(found.Span.Start);
    }

    private void ClearMark()
    {
        _marked = null;

        _markedCalls.Clear();
    }

    private void DrawMark(SKCanvas canvas, int width, int height)
    {
        if (GetMarkedCall() is not { } marked)
        {
            ShowMinimap(null, width, height);

            return;
        }

        var x = (float)((marked.Span.Start - _viewStart) * PixelsPerUnit(width));

        if (x < 0 || x > width || GetMarkedTop(marked) is not { } rowTop)
        {
            ShowMinimap(marked, width, height);

            return;
        }

        var bottom = height - BandHeight;

        canvas.DrawLine(x, Math.Max(ContentTop, Math.Min(rowTop, bottom)), x, bottom, _paints.Mark);

        ShowMinimap(null, width, height);
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

                var index = row.IndexAt(_axis, marked.Span.Start, 0);

                if (index < 0 || row.CallAt(index) != marked.Span.Call || row.NodeAt(index) != marked.Span.Node)
                {
                    continue;
                }

                return ContentTop + _laneTops[lane] - (float)_scrollY + LaneHeaderHeight + Math.Min(depth, DepthOf(thread)) * _rowHeight;
            }

            return null;
        }

        return null;
    }

    private void ShowMinimap(MarkedCall? marked, int width, int height)
    {
        var fits = width >= MinimapWidth + MinimapMargin * 2 && height - BandHeight - ContentTop >= MinimapHeight + MinimapMargin * 2;

        if (marked is not { } call || !fits)
        {
            _minimap.Visibility = Visibility.Collapsed;

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

        var colour = _paints.Mark.Color;

        if (_minimapMarkers is not { } shown || !shown.Current.Equals(current) || shown.Marked != call || shown.Colour != colour)
        {
            var markers = new List<MinimapMarker>();

            if (!double.IsNaN(current))
            {
                markers.Add(new MinimapMarker(current, CurrentColour));
            }

            markers.Add(new MinimapMarker(call.Span.Start,
                                          Color.FromArgb(colour.Alpha, colour.Red, colour.Green, colour.Blue),
                                          call.Lane,
                                          call.Depth));

            _minimap.Markers = markers;

            _minimapMarkers = (current, call, colour);
        }

        _minimap.Visibility = Visibility.Visible;
    }

    private MarkedCall? GetMarkedCall() => _marked is { } marked ? GetCall(marked.Node, marked.Call) : null;

    private MarkedCall? GetCall(CallStackNode node, int call)
    {
        if (call < 0 || Overview is not { } timeline)
        {
            return null;
        }

        var key = (node, call, _axis);

        if (!_markedCalls.TryGetValue(key, out var found))
        {
            found = FindCall(timeline, node, call);

            _markedCalls[key] = found;
        }

        return found;
    }

    private MarkedCall? FindCall(TraceTimeline timeline, CallStackNode node, int call)
    {
        if (Matches(timeline, n => ReferenceEquals(n, node)) is not { } nodes)
        {
            return null;
        }

        for (var lane = 0; lane < timeline.Threads.Count; lane++)
        {
            var thread = timeline.Threads[lane];

            for (var depth = 0; depth < thread.Rows.Count; depth++)
            {
                var row = thread.Rows[depth];

                for (var index = 0; index < row.Count; index++)
                {
                    if (row.CallAt(index) != call)
                    {
                        continue;
                    }

                    var spanNode = row.NodeAt(index);

                    if (spanNode >= 0 && spanNode < nodes.Length && nodes[spanNode])
                    {
                        return new MarkedCall(row.Span(_axis, index), thread.ThreadId, lane, depth);
                    }
                }
            }
        }

        return null;
    }

    private readonly record struct MarkedCall(TraceTimelineSpan Span, uint ThreadId, int Lane, int Depth);
}
