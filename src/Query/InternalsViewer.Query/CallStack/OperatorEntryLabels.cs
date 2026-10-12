using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.CallStack;

public static class OperatorEntryLabels
{
    public static void Apply(IEnumerable<EngineEvent> events)
    {
        var byFrame = new Dictionary<CallStackNode, List<ExecutionOperatorEvent>>(ReferenceEqualityComparer.Instance);

        foreach (var operatorEvent in events.OfType<ExecutionOperatorEvent>().Where(o => o.PlanNodeIdentifier is { NodeId: >= 0 }))
        {
            foreach (var frame in operatorEvent.EntryFrames)
            {
                if (!byFrame.TryGetValue(frame, out var operators))
                {
                    operators = [];

                    byFrame[frame] = operators;
                }

                if (!operators.Any(o => ReferenceEquals(o, operatorEvent)))
                {
                    operators.Add(operatorEvent);
                }
            }
        }

        foreach (var (frame, operators) in byFrame)
        {
            frame.PlanOperators = operators;
        }
    }
}
