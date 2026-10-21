using System;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Trace;

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

    private readonly Dictionary<(CallStackNode Node, int Call, TraceTimelineAxis Axis), MarkedCall?> _markedCalls = [];

    private (CallStackNode Node, int Call)? _marked;

    private (double Current, MarkedCall Marked)? _minimapMarkers;

    private TraceTimeline? Overview => FullTimeline ?? _timeline;

    public void MarkCall(CallStackNode? node, int call)
    {
        _marked = node is not null && call >= 0 ? (node, call) : null;

        _canvas.Invalidate();
    }

    public void MovePlayheadTo(CallStackNode node, int call)
    {
        if (RevealCall(node, call) is not { } start)
        {
            return;
        }

        StopPlay();

        PlacePlayhead(start);
    }

    public void ZoomToCall(CallStackNode node, int call)
    {
        if (RevealSpan(node, call) is { } span)
        {
            ZoomTo(span);
        }
    }

    private double? RevealCall(CallStackNode node, int call)
        => RevealSpan(node, call) is { } span ? TraceScale.GetValue(span.From, _axis) : null;

    private TraceSpan? RevealSpan(CallStackNode node, int call)
    {
        if (GetCall(node, call) is not { } found)
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

        _markedCalls.Clear();
    }

    private void DrawMark(SKCanvas canvas, int width, int height)
    {
        if (GetMarkedCall() is not { } marked)
        {
            ShowMinimap(null, width, height);

            return;
        }

        var x = (float)((TraceScale.GetValue(marked.Span.From, _axis) - _viewStart) * PixelsPerUnit(width));

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

                var index = row.GetIndex(marked.Span.From.Position);

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

        if (_minimapMarkers is not { } shown || !shown.Current.Equals(current) || shown.Marked != call)
        {
            var markers = new List<MinimapMarker>();

            if (!double.IsNaN(current))
            {
                markers.Add(new MinimapMarker(current, CurrentColour));
            }

            markers.Add(new MinimapMarker(TraceScale.GetValue(call.Span.From, _axis), MarkedColour, call.Lane, call.Depth));

            _minimap.Markers = markers;

            _minimapMarkers = (current, call);
        }

        _minimap.Visibility = Visibility.Visible;
    }

    private void ReleaseMinimap()
    {
        _minimap.Timeline = null;
        _minimap.Crop = null;
        _minimap.Scope = null;
        _minimap.Markers = null;

        _minimapMarkers = null;
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
            found = FindCall(timeline, node, call) is { } hit ? MarkedCallAt(timeline, hit) : null;

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
