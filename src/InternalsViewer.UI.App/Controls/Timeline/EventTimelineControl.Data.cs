using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Plans;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    private static IEnumerable<EngineEvent> ExpandGroupedEvents(List<EngineEvent> events)
    {
        foreach (var engineEvent in events)
        {
            yield return engineEvent;

            if (engineEvent is ReadEventGroup readGroup)
            {
                foreach (var member in readGroup.Events)
                {
                    if (member is not IoEvent and not FileEvent)
                    {
                        yield return member;
                    }
                }
            }
        }
    }

    private void BuildTimes()
    {
        _times = new List<double>(_sortedEvents.Count);

        if (_sortedEvents.Count == 0)
        {
            _minTime = 0;
            _maxTime = 1;
            _timeRange = 1;

            return;
        }

        var min = double.MaxValue;
        var max = double.MinValue;

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            var ev = _sortedEvents[i];
            var start = StartMs(ev);
            _times.Add(start);

            if (start < min)
            {
                min = start;
            }

            // Operators occupy [start, start + duration]; point events are an instant at start. Events
            // sharing a coarse timestamp are already spread to distinct times upstream (see
            // EventReader.SpreadEvents), so nothing is fanned out here.
            var end = ev is ExecutionOperatorEvent ? start + DurationMs(ev) : start;

            if (end > max)
            {
                max = end;
            }
        }

        // Apply the optional crop: a set Start/EndOffset (microseconds) overrides the natural event
        // extent so that activity outside the cropped window falls off the axis (clipped by the canvas).
        _minTime = StartOffset.HasValue ? StartOffset.Value / 1000.0 : min;
        _maxTime = EndOffset.HasValue ? EndOffset.Value / 1000.0 : max;
        _timeRange = Math.Max(_maxTime - _minTime, 1.0);
    }

    private void BuildOperatorLayout()
    {
        var operators = new List<(int Index, ExecutionOperatorEvent Op)>();

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            if (_sortedEvents[i] is ExecutionOperatorEvent op)
            {
                operators.Add((i, op));
            }
        }

        _orderedOperators = [.. operators.OrderBy(o => o.Op.NodeLevel)
                                         .ThenBy(o => _times[o.Index])
                                         .ThenBy(o => o.Op.PlanNodeIdentifier?.NodeId ?? 0)];

        _maxCost = operators.Count > 0
            ? operators.Where(o => o.Op.NodeLevel > 0).Select(o => o.Op.Cost ?? 0).DefaultIfEmpty(0).Max()
            : 0;

        _maxRows = operators.Count > 0
            ? operators.Where(o => o.Op.Category == OperatorCategory.DataAccess).Select(o => o.Op.RowsProcessed).DefaultIfEmpty(0).Max()
            : 0;
    }

    private void RebuildRows() => _rows.Rebuild(_sortedEvents, ShowLocks, ShowLatches, ShowWaits, _labelFont);
}
