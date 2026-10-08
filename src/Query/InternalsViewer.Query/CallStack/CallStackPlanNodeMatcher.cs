using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.CallStack;

/// <summary>
/// Attributes events that carry only a call stack to the innermost plan operator whose entry frame is on that stack
/// </summary>
/// <remarks>
/// Runs after <see cref="OperatorCallStackMatcher"/> has found each operator's entry frames. An event the plan node
/// matcher could not place — a scheduler yield, a batch sync point, an object pool lookup — still has a stack, and walking
/// up from its innermost frame the first entry frame met belongs to the operator that was running. Events that already
/// have a node keep it.
///
/// Where one entry frame is shared by several operators, the merged-sibling case, the thread profile windows arbitrate by
/// time. If they cannot, the event is left unattributed rather than guessed.
/// </remarks>
public static class CallStackPlanNodeMatcher
{
    public static void Match(IReadOnlyList<EngineEvent> events)
    {
        var ownersByFrame = new Dictionary<CallStackNode, List<ExecutionOperatorEvent>>();

        foreach (var operatorEvent in events.OfType<ExecutionOperatorEvent>())
        {
            if (operatorEvent.PlanNodeIdentifier is null)
            {
                continue;
            }

            foreach (var frame in operatorEvent.EntryFrames)
            {
                if (!ownersByFrame.TryGetValue(frame, out var owners))
                {
                    owners = [];

                    ownersByFrame[frame] = owners;
                }

                owners.Add(operatorEvent);
            }
        }

        if (ownersByFrame.Count == 0)
        {
            return;
        }

        var windows = BuildWindows(events);

        foreach (var engineEvent in events)
        {
            if (engineEvent is ExecutionOperatorEvent || engineEvent.PlanNodeIdentifier is not null || engineEvent.CallStack is null)
            {
                continue;
            }

            foreach (var frame in engineEvent.CallStack.Ancestors())
            {
                if (!ownersByFrame.TryGetValue(frame, out var owners))
                {
                    continue;
                }

                var owner = owners.Count == 1 ? owners[0] : Arbitrate(owners, engineEvent, windows);

                if (owner is not null)
                {
                    engineEvent.PlanNodeIdentifier = owner.PlanNodeIdentifier;
                }

                break;
            }
        }
    }

    private static ExecutionOperatorEvent? Arbitrate(List<ExecutionOperatorEvent> owners,
                                                     EngineEvent engineEvent,
                                                     Dictionary<int, (DateTime Start, DateTime End)> windows)
    {
        ExecutionOperatorEvent? match = null;

        foreach (var owner in owners)
        {
            if (!windows.TryGetValue(owner.PlanNodeIdentifier!.NodeId, out var window)
                || engineEvent.Timestamp < window.Start
                || engineEvent.Timestamp > window.End)
            {
                continue;
            }

            if (match is not null)
            {
                return null;
            }

            match = owner;
        }

        return match;
    }

    private static Dictionary<int, (DateTime Start, DateTime End)> BuildWindows(IReadOnlyList<EngineEvent> events)
    {
        var windows = new Dictionary<int, (DateTime Start, DateTime End)>();

        foreach (var threadEvent in events.OfType<QueryThreadEvent>())
        {
            var end = threadEvent.Timestamp;

            var start = end - TimeSpan.FromMicroseconds(Math.Max(0, threadEvent.DurationUs));

            windows[threadEvent.NodeId] = windows.TryGetValue(threadEvent.NodeId, out var existing)
                ? (start < existing.Start ? start : existing.Start, end > existing.End ? end : existing.End)
                : (start, end);
        }

        return windows;
    }
}
