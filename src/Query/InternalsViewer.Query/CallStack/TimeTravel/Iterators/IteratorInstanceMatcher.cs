using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.CallStack.TimeTravel.Iterators;

public static class IteratorInstanceMatcher
{
    public static int Match(CallStackTree callStack, IReadOnlyList<EngineEvent> events, IteratorPlanLink planLink)
    {
        var hierarchy = OperatorHierarchy.Build(events);

        foreach (var operatorEvent in hierarchy.Operators)
        {
            operatorEvent.EntryFrames = [];

            operatorEvent.Instances = [];
        }

        var assigned = new Dictionary<ExecutionOperatorEvent, List<IteratorInstance>>(ReferenceEqualityComparer.Instance);

        foreach (var instance in IteratorInstance.Build(callStack).Where(i => !i.IsWrapper))
        {
            if (planLink.NodeOf(instance.Address) is not { } node
                || hierarchy.At(node) is not { PlanNodeIdentifier.NodeId: >= 0 } operatorEvent)
            {
                continue;
            }

            if (!assigned.TryGetValue(operatorEvent, out var linked))
            {
                linked = [];

                assigned[operatorEvent] = linked;
            }

            linked.Add(instance);
        }

        foreach (var (operatorEvent, instances) in assigned)
        {
            operatorEvent.EntryFrames = [.. instances.SelectMany(i => i.Entries).Distinct().OrderBy(e => e.Order)];

            operatorEvent.Instances = [.. instances.Select(i => i.Address).Distinct()];
        }

        AssignStatements(hierarchy, assigned);

        foreach (var operatorEvent in hierarchy.Operators)
        {
            operatorEvent.ExitFrames = ExitFrames(operatorEvent, hierarchy);
        }

        return assigned.Count;
    }

    private static void AssignStatements(OperatorHierarchy hierarchy,
                                         Dictionary<ExecutionOperatorEvent, List<IteratorInstance>> assigned)
    {
        foreach (var statement in hierarchy.Operators.Where(o => o.PlanNodeIdentifier!.NodeId < 0))
        {
            var entries = NearestAssigned(hierarchy, statement, assigned)
                              .SelectMany(o => o.EntryFrames)
                              .Select(e => e.Ancestors().Skip(1).FirstOrDefault(a => a.IsEntryFrameFor(statement.Name)))
                              .OfType<CallStackNode>()
                              .Distinct()
                              .OrderBy(e => e.Order)
                              .ToList();

            if (entries.Count > 0)
            {
                statement.EntryFrames = entries;
            }
        }
    }

    private static IEnumerable<ExecutionOperatorEvent> NearestAssigned(OperatorHierarchy hierarchy,
                                                                       ExecutionOperatorEvent operatorEvent,
                                                                       Dictionary<ExecutionOperatorEvent, List<IteratorInstance>> assigned)
    {
        foreach (var child in hierarchy.Children(operatorEvent))
        {
            if (assigned.ContainsKey(child))
            {
                yield return child;

                continue;
            }

            foreach (var descendant in NearestAssigned(hierarchy, child, assigned))
            {
                yield return descendant;
            }
        }
    }

    private static List<CallStackNode> ExitFrames(ExecutionOperatorEvent operatorEvent, OperatorHierarchy hierarchy)
        => [.. hierarchy.Descendants(operatorEvent)
                        .SelectMany(d => d.EntryFrames)
                        .Where(e => !operatorEvent.EntryFrames.Contains(e))
                        .Distinct()];

    private sealed class IteratorInstance(ulong address)
    {
        public ulong Address { get; } = address;

        public List<CallStackNode> Entries { get; } = [];

        public bool IsWrapper => !Entries.Any(e => e.IsOperatorBoundary) && Entries.Any(e => e.HasOperator);

        public static IReadOnlyCollection<IteratorInstance> Build(CallStackTree callStack)
        {
            var instances = new Dictionary<ulong, IteratorInstance>();

            var pending = new Stack<(CallStackNode Node, ulong Context)>();

            foreach (var child in callStack.Root.ChildNodes)
            {
                pending.Push((child, 0));
            }

            while (pending.Count > 0)
            {
                var (node, context) = pending.Pop();

                if (node.Frame is { Instance: not 0 } frame
                    && frame.Instance != context
                    && !IteratorMethods.IsPlanObjectClass(frame.Resolved?.ClassName))
                {
                    if (!instances.TryGetValue(frame.Instance, out var instance))
                    {
                        instance = new IteratorInstance(frame.Instance);

                        instances[frame.Instance] = instance;
                    }

                    instance.Entries.Add(node);

                    context = frame.Instance;
                }

                foreach (var child in node.ChildNodes)
                {
                    pending.Push((child, context));
                }
            }

            return instances.Values;
        }
    }
}
