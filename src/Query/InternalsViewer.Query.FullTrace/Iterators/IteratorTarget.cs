using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Iterators;

public sealed class IteratorTarget(CallTreeNode node,
                                   ExecutionOperatorEvent? operatorEvent,
                                   IteratorTargetType type = IteratorTargetType.Iterator)
{
    public CallTreeNode Node { get; } = node;

    public ExecutionOperatorEvent? Operator { get; } = operatorEvent;

    public IteratorTargetType Type { get; } = type;

    public string TypeName => Type switch
    {
        IteratorTargetType.PlanObject => "Plan Object",
        IteratorTargetType.ProfilingIterator => "Profiling Iterator",
        _ => "Iterator"
    };

    public string Label => Operator is { PlanNodeIdentifier: not null } operatorEvent
        ? operatorEvent.Label
        : Node.Frame?.Function?.ClassName is { Length: > 0 } className ? className : Node.Symbol;

    public static IReadOnlyDictionary<ulong, IteratorTarget> Build(CallTree? tree,
                                                                   IReadOnlyList<EngineEvent> events,
                                                                   IteratorPlanLink planLink,
                                                                   OperatorFrameMap frames)
    {
        var hierarchy = OperatorHierarchy.Build(events);

        var outermost = GetOutermostFrames(tree);

        var targets = new Dictionary<ulong, IteratorTarget>();

        foreach (var (address, planNode, type) in GetLinked(planLink))
        {
            var operatorEvent = hierarchy.At(planNode);

            var entry = operatorEvent is null ? null : frames.GetEntryFrames(operatorEvent).FirstOrDefault();

            var own = outermost.GetValueOrDefault(address);

            if ((own ?? entry) is { } frame)
            {
                targets.TryAdd(address, new IteratorTarget(frame, operatorEvent, GetTargetType(type, own)));
            }
        }

        foreach (var (address, frame) in outermost)
        {
            targets.TryAdd(address, new IteratorTarget(frame, null, GetTargetType(IteratorTargetType.Iterator, frame)));
        }

        return targets;
    }

    private static IEnumerable<(ulong Address, PlanNodeIdentifier Node, IteratorTargetType Type)> GetLinked(IteratorPlanLink planLink)
        => planLink.Instances.Select(i => (i.Key, i.Value, IteratorTargetType.Iterator))
                   .Concat(planLink.PlanObjects.Select(p => (p.Key, p.Value, IteratorTargetType.PlanObject)));

    private static IteratorTargetType GetTargetType(IteratorTargetType type, CallTreeNode? own)
        => type == IteratorTargetType.Iterator && IteratorSymbolLookup.IsProfilingIterator(own?.Frame)
            ? IteratorTargetType.ProfilingIterator
            : type;

    private static Dictionary<ulong, CallTreeNode> GetOutermostFrames(CallTree? tree)
    {
        var shallowest = new Dictionary<ulong, (CallTreeNode Node, int Depth)>();

        foreach (var node in tree?.GetNodes() ?? [])
        {
            if (node.Frame is not { InstanceAddress: not 0 and var instance })
            {
                continue;
            }

            var depth = node.GetAncestors().Count();

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
