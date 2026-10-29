using System.Collections.Generic;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const int MaximumHistory = 100;

    private readonly List<TimelineLocation> _backHistory = [];

    private readonly List<TimelineLocation> _forwardHistory = [];

    public void GoBack() => Travel(_backHistory, _forwardHistory);

    public void GoForward() => Travel(_forwardHistory, _backHistory);

    private void RememberLocation()
    {
        if (GetLocation() is not { } location || (_backHistory.Count > 0 && _backHistory[^1] == location))
        {
            return;
        }

        _backHistory.Add(location);

        if (_backHistory.Count > MaximumHistory)
        {
            _backHistory.RemoveAt(0);
        }

        _forwardHistory.Clear();
    }

    private void Travel(List<TimelineLocation> from, List<TimelineLocation> to)
    {
        var current = GetLocation();

        while (from.Count > 0)
        {
            var location = from[^1];

            from.RemoveAt(from.Count - 1);

            if (!Restore(location))
            {
                continue;
            }

            if (current is { } previous)
            {
                to.Add(previous);
            }

            return;
        }
    }

    private TimelineLocation? GetLocation()
    {
        if (GetCallReference(_selected) is { } selected)
        {
            return new TimelineLocation(null, _axis, selected.Node, selected.CallIndex);
        }

        return _playhead is { } playhead ? new TimelineLocation(playhead, _axis, null, -1) : null;
    }

    private bool Restore(TimelineLocation location)
    {
        if (location.Node is { } node)
        {
            if (_visible is not { } timeline || FindCall(timeline, node, location.CallIndex) is not { } hit)
            {
                return false;
            }

            StopPlay();

            SelectAndShow(hit);

            return true;
        }

        if (location.Playhead is not { } value || location.Axis != _axis)
        {
            return false;
        }

        StopPlay();

        PlacePlayhead(value);

        return true;
    }

    private readonly record struct TimelineLocation(double? Playhead, TraceTimelineAxis Axis, CallStackNode? Node, int CallIndex);
}
