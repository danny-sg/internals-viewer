using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Iterators;

public sealed class OperatorFrameMap
{
    public static OperatorFrameMap Empty { get; } = new();

    public IReadOnlyDictionary<ulong, ExecutionOperatorEvent> OperatorsByInstance => Owners;

    private Dictionary<ulong, ExecutionOperatorEvent> Owners { get; } = [];

    private Dictionary<ExecutionOperatorEvent, IReadOnlyList<ulong>> Instances { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<ExecutionOperatorEvent, IReadOnlyList<CallTreeNode>> Entries { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<ExecutionOperatorEvent, IReadOnlyList<CallTreeNode>> Exits { get; } = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<ulong> GetInstances(ExecutionOperatorEvent operatorEvent)
        => Instances.GetValueOrDefault(operatorEvent) ?? [];

    public IReadOnlyList<CallTreeNode> GetEntryFrames(ExecutionOperatorEvent operatorEvent)
        => Entries.GetValueOrDefault(operatorEvent) ?? [];

    public IReadOnlyList<CallTreeNode> GetExitFrames(ExecutionOperatorEvent operatorEvent)
        => Exits.GetValueOrDefault(operatorEvent) ?? [];

    internal static OperatorFrameMap Build(CallTree callTree, IReadOnlyList<EngineEvent> events, IteratorPlanLink planLink)
    {
        var hierarchy = OperatorHierarchy.Build(events);

        var frames = new OperatorFrameMap();

        var entries = GetInstanceEntries(callTree);

        var assigned = new Dictionary<ExecutionOperatorEvent, List<ulong>>(ReferenceEqualityComparer.Instance);

        foreach (var (instance, instanceEntries) in entries)
        {
            if (IsWrapper(instanceEntries)
                || planLink.GetNode(instance) is not { } node
                || hierarchy.At(node) is not { PlanNodeIdentifier.NodeId: >= 0 } operatorEvent)
            {
                continue;
            }

            ref var instances = ref CollectionsMarshal.GetValueRefOrAddDefault(assigned, operatorEvent, out _);

            (instances ??= []).Add(instance);
        }

        foreach (var (operatorEvent, instances) in assigned)
        {
            frames.SetInstances(operatorEvent, instances);

            frames.SetEntryFrames(operatorEvent, [.. instances.SelectMany(i => entries[i]).Distinct().OrderBy(e => e.Order)]);
        }

        frames.SetStatementFrames(hierarchy);

        frames.SetExitFrames(hierarchy);

        frames.LabelEntryFrames(hierarchy.Operators);

        return frames;
    }

    internal void SetInstances(ExecutionOperatorEvent operatorEvent, IReadOnlyList<ulong> instances)
    {
        Instances[operatorEvent] = instances;

        foreach (var instance in instances)
        {
            Owners.TryAdd(instance, operatorEvent);
        }
    }

    internal void SetEntryFrames(ExecutionOperatorEvent operatorEvent, IReadOnlyList<CallTreeNode> frames)
        => Entries[operatorEvent] = frames;

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

    private static Dictionary<ulong, List<CallTreeNode>> GetInstanceEntries(CallTree callTree)
    {
        var entries = new Dictionary<ulong, List<CallTreeNode>>();

        var pending = new Stack<(CallTreeNode Node, ulong Context)>();

        foreach (var child in callTree.Root.ChildNodes)
        {
            pending.Push((child, 0));
        }

        while (pending.Count > 0)
        {
            var (node, context) = pending.Pop();

            if (node.Frame is { InstanceAddress: not 0 } frame
                && frame.InstanceAddress != context
                && !IteratorSymbolLookup.IsPlanObject(frame))
            {
                ref var instanceEntries = ref CollectionsMarshal.GetValueRefOrAddDefault(entries, frame.InstanceAddress, out _);

                (instanceEntries ??= []).Add(node);

                context = frame.InstanceAddress;
            }

            foreach (var child in node.ChildNodes)
            {
                pending.Push((child, context));
            }
        }

        return entries;
    }

    private static bool IsWrapper(List<CallTreeNode> entries) => !entries.Any(e => e.IsOperatorBoundary) && entries.Any(e => e.HasOperatorName);

    private void SetStatementFrames(OperatorHierarchy hierarchy)
    {
        foreach (var statement in hierarchy.Operators.Where(o => o.PlanNodeIdentifier!.NodeId < 0))
        {
            var entries = GetNearestWithInstances(hierarchy, statement)
                              .SelectMany(GetEntryFrames)
                              .Select(e => e.GetAncestors().Skip(1).FirstOrDefault(a => a.IsEntryFrameFor(statement.Name)))
                              .OfType<CallTreeNode>()
                              .Distinct()
                              .OrderBy(e => e.Order)
                              .ToList();

            if (entries.Count > 0)
            {
                SetEntryFrames(statement, entries);
            }
        }
    }

    private IEnumerable<ExecutionOperatorEvent> GetNearestWithInstances(OperatorHierarchy hierarchy, ExecutionOperatorEvent operatorEvent)
    {
        foreach (var child in hierarchy.Children(operatorEvent))
        {
            if (Instances.ContainsKey(child))
            {
                yield return child;

                continue;
            }

            foreach (var descendant in GetNearestWithInstances(hierarchy, child))
            {
                yield return descendant;
            }
        }
    }

    private void SetExitFrames(OperatorHierarchy hierarchy)
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
}
