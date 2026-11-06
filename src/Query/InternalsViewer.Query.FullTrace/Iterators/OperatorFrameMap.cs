using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Iterators;

public sealed class OperatorFrameMap
{
    public static OperatorFrameMap Empty { get; } = new();

    private Dictionary<ExecutionOperatorEvent, IReadOnlyList<CallTreeNode>> Entries { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<ExecutionOperatorEvent, IReadOnlyList<CallTreeNode>> Exits { get; } = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<CallTreeNode> GetEntryFrames(ExecutionOperatorEvent operatorEvent)
        => Entries.GetValueOrDefault(operatorEvent) ?? [];

    public IReadOnlyList<CallTreeNode> GetExitFrames(ExecutionOperatorEvent operatorEvent)
        => Exits.GetValueOrDefault(operatorEvent) ?? [];

    internal void SetEntryFrames(ExecutionOperatorEvent operatorEvent, IReadOnlyList<CallTreeNode> frames)
        => Entries[operatorEvent] = frames;

    internal void SetExitFrames(OperatorHierarchy hierarchy)
    {
        foreach (var operatorEvent in hierarchy.Operators)
        {
            var entries = GetEntryFrames(operatorEvent);

            Exits[operatorEvent] = [.. hierarchy.Descendants(operatorEvent)
                                                .SelectMany(GetEntryFrames)
                                                .Where(e => !entries.Contains(e))
                                                .Distinct()];
        }
    }

    internal void LabelEntryFrames(IEnumerable<ExecutionOperatorEvent> operators)
    {
        var byFrame = new Dictionary<CallTreeNode, List<ExecutionOperatorEvent>>(ReferenceEqualityComparer.Instance);

        foreach (var operatorEvent in operators.Where(o => o.PlanNodeIdentifier is { NodeId: >= 0 }))
        {
            foreach (var frame in GetEntryFrames(operatorEvent))
            {
                ref var entered = ref CollectionsMarshal.GetValueRefOrAddDefault(byFrame, frame, out _);

                entered ??= [];

                if (!entered.Any(o => ReferenceEquals(o, operatorEvent)))
                {
                    entered.Add(operatorEvent);
                }
            }
        }

        foreach (var (frame, entered) in byFrame)
        {
            frame.PlanOperators = entered;
        }
    }
}
