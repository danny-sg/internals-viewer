using System.Collections.Generic;
using System.Linq;
using System;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Controls.Timeline.Definition;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    /// <summary>
    /// Enumerates the physical file reads, including those folded into a read group
    /// </summary>
    /// <remarks>
    /// Unlike the other audio sweeps this can't read the top-level list alone: a file read is normally a member of the
    /// <see cref="ReadEventGroup"/> built around it, so it only surfaces at the top level when consolidation left it
    /// unpaired.
    /// </remarks>
    private static IEnumerable<FileEvent> EnumerateFileReads(IReadOnlyList<EngineEvent> events)
    {
        foreach (var engineEvent in events)
        {
            switch (engineEvent)
            {
                case FileEvent { IsRead: true } fileEvent:
                    yield return fileEvent;

                    break;

                case ReadEventGroup readGroup:
                    foreach (var member in readGroup.Events)
                    {
                        if (member is FileEvent { IsRead: true } fileMember)
                        {
                            yield return fileMember;
                        }
                    }

                    break;
            }
        }
    }

    private void BuildTimes()
    {
        _times = new List<double>(_definition.Events.Count);

        if (_definition.Events.Count == 0)
        {
            _minTime = 0;
            _maxTime = 1;
            _timeRange = 1;

            return;
        }

        var min = double.MaxValue;
        var max = double.MinValue;

        for (var i = 0; i < _definition.Events.Count; i++)
        {
            var ev = _definition.Events[i];
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

        // The axis is simply the extent of the events given to the control — any windowing (the query crop, its
        // padding) is the caller's job upstream; the timeline knows nothing about it.
        _minTime = min;
        _maxTime = max;
        _timeRange = Math.Max(_maxTime - _minTime, 1.0);
    }

    private void BuildOperatorLayout()
    {
        var operators = new List<(int Index, ExecutionOperatorEvent Op)>();

        for (var i = 0; i < _definition.Events.Count; i++)
        {
            if (_definition.Events[i] is ExecutionOperatorEvent op)
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

    private void RebuildBands() => _bands.Rebuild(_definition, _renderResource.LabelFont);

    private void BuildAudioCues()
    {
        var events = _definition.Events;

        var readMembers = new HashSet<EngineEvent>(events.OfType<ReadEventGroup>().SelectMany(g => g.Events),
                                                   ReferenceEqualityComparer.Instance);

        _readEventsByTime = [.. events.OfType<ReadEventGroup>().OrderBy(read => read.TimeUs)];

        _latchEventsByTime = [.. events.OfType<LatchEvent>().Where(l => !readMembers.Contains(l)).OrderBy(latch => latch.TimeUs)];

        _fileReadEventsByTime = [.. EnumerateFileReads(events).OrderBy(read => read.TimeUs)];
    }
}
