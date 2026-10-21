using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Iterators;

public sealed class IteratorTarget(CallStackNode node, ExecutionOperatorEvent? operatorEvent, bool isPlanObject = false)
{
    public CallStackNode Node { get; } = node;

    public ExecutionOperatorEvent? Operator { get; } = operatorEvent;

    public bool IsPlanObject { get; } = isPlanObject;

    public string Label => Operator is { PlanNodeIdentifier: not null } operatorEvent
        ? operatorEvent.Label
        : Node.Frame?.Resolved?.ClassName is { Length: > 0 } className ? className : Node.Symbol;

    public static IReadOnlyDictionary<ulong, IteratorTarget> Build(CallStackTree? tree,
                                                                   IReadOnlyList<EngineEvent> events,
                                                                   IteratorPlanLink planLink)
    {
        var hierarchy = OperatorHierarchy.Build(events);

        var outermost = OutermostFrames(tree);

        var targets = new Dictionary<ulong, IteratorTarget>();

        foreach (var (address, planNode, isPlanObject) in Linked(planLink))
        {
            var operatorEvent = hierarchy.At(planNode);

            if ((outermost.GetValueOrDefault(address) ?? operatorEvent?.EntryFrames.FirstOrDefault()) is { } frame)
            {
                targets.TryAdd(address, new IteratorTarget(frame, operatorEvent, isPlanObject));
            }
        }

        foreach (var (address, frame) in outermost)
        {
            targets.TryAdd(address, new IteratorTarget(frame, null));
        }

        return targets;
    }

    private static IEnumerable<(ulong Address, PlanNodeIdentifier Node, bool IsPlanObject)> Linked(IteratorPlanLink planLink)
        => planLink.Instances.Select(i => (i.Key, i.Value, false)).Concat(planLink.PlanObjects.Select(p => (p.Key, p.Value, true)));

    private static Dictionary<ulong, CallStackNode> OutermostFrames(CallStackTree? tree)
    {
        var shallowest = new Dictionary<ulong, (CallStackNode Node, int Depth)>();

        foreach (var node in tree?.Nodes() ?? [])
        {
            if (node.Frame is not { Instance: not 0 and var instance })
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

        return shallowest.ToDictionary(s => s.Key, s => s.Value.Node);
    }
}
