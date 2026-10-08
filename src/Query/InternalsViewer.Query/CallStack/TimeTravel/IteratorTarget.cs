using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed class IteratorTarget(CallStackNode node, ExecutionOperatorEvent? operatorEvent)
{
    public CallStackNode Node { get; } = node;

    public ExecutionOperatorEvent? Operator { get; } = operatorEvent;

    public string Label => Operator is { PlanNodeIdentifier: { } id } operatorEvent
        ? $"{operatorEvent.OperatorDescription} (Node {id.NodeId})"
        : Node.Frame?.Resolved?.ClassName is { Length: > 0 } className ? className : Node.Symbol;

    public static IReadOnlyDictionary<ulong, IteratorTarget> Build(CallStackTree? tree, IReadOnlyList<EngineEvent> events)
    {
        var targets = new Dictionary<ulong, IteratorTarget>();

        var operators = events.OfType<ExecutionOperatorEvent>()
                              .OrderBy(o => o.PlanNodeIdentifier is { NodeId: < 0 })
                              .ToList();

        foreach (var operatorEvent in operators)
        {
            foreach (var entry in operatorEvent.EntryFrames.OrderBy(e => e.Order))
            {
                if (entry.Frame is { Instance: not 0 and var instance })
                {
                    targets.TryAdd(instance, new IteratorTarget(entry, operatorEvent));
                }
            }
        }

        var shallowest = new Dictionary<ulong, (CallStackNode Node, int Depth)>();

        foreach (var node in tree?.Nodes() ?? [])
        {
            if (node.Frame is not { Instance: not 0 and var instance } || targets.ContainsKey(instance))
            {
                continue;
            }

            var depth = node.Ancestors().Count();

            if (!shallowest.TryGetValue(instance, out var current)
                || depth < current.Depth
                || (depth == current.Depth && node.Order < current.Node.Order))
            {
                shallowest[instance] = (node, depth);
            }
        }

        foreach (var (instance, (node, _)) in shallowest)
        {
            targets[instance] = new IteratorTarget(node, null);
        }

        return targets;
    }
}
