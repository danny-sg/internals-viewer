using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Events.Operators;

/// <summary>
/// Pulls each operator's start back to the earliest event attributed to it or to any operator beneath it
/// </summary>
/// <remarks>
/// Operators are timed before events are attributed through their call stacks and before pool misses are given their
/// construction spans, so an operator can end up starting after work that belongs to it. An operator always bounds its
/// own events and its children, so the start moves and the end stays where it was measured.
/// </remarks>
public static class OperatorBoundsExtender
{
    public static void ExtendStarts(IReadOnlyList<EngineEvent> events)
    {
        var hierarchy = OperatorHierarchy.Build(events);

        if (hierarchy.Operators.Count == 0)
        {
            return;
        }

        var earliest = new Dictionary<PlanNodeIdentifier, long>();

        foreach (var engineEvent in events)
        {
            if (!OperatorEventBuilder.IsDataAccess(engineEvent) || engineEvent.PlanNodeIdentifier is not { } node)
            {
                continue;
            }

            if (!earliest.TryGetValue(node, out var current) || engineEvent.TimeUs < current)
            {
                earliest[node] = engineEvent.TimeUs;
            }
        }

        foreach (var root in hierarchy.Roots)
        {
            Extend(root, hierarchy, earliest);
        }
    }

    private static long Extend(ExecutionOperatorEvent operatorEvent,
                               OperatorHierarchy hierarchy,
                               Dictionary<PlanNodeIdentifier, long> earliest)
    {
        var start = operatorEvent.TimeUs;

        foreach (var child in hierarchy.Children(operatorEvent))
        {
            start = Math.Min(start, Extend(child, hierarchy, earliest));
        }

        if (operatorEvent.PlanNodeIdentifier is { } node && earliest.TryGetValue(node, out var firstEvent))
        {
            start = Math.Min(start, firstEvent);
        }

        if (start < operatorEvent.TimeUs)
        {
            var shift = operatorEvent.TimeUs - start;

            operatorEvent.DurationUs += shift;
            operatorEvent.TimeUs = start;

            if (operatorEvent.Timestamp != default)
            {
                operatorEvent.Timestamp -= TimeSpan.FromMicroseconds(shift);
            }
        }

        return operatorEvent.TimeUs;
    }
}
