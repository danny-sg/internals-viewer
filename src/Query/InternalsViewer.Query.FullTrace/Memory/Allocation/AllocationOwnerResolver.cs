using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

internal static class AllocationOwnerResolver
{
    private const int InstanceSlot = 0;

    public static ulong[] GetOwners(IReadOnlyList<MemoryBlock> blocks,
                                    TraceCallStore calls,
                                    IReadOnlySet<ulong> iterators,
                                    IReadOnlyList<IteratorPlanNode> planNodes)
    {
        var owners = EnclosingSweep.GetEnclosing(GetCalls(calls, iterators, GetBuilders(planNodes)),
                                                 [.. blocks.Select(b => new SweepSpan<TracePosition>(b.Thread, b.Start, b.Start, 0))]);

        for (var index = 0; index < blocks.Count; index++)
        {
            if (iterators.Contains(blocks[index].Pointer))
            {
                owners[index] = blocks[index].Pointer;
            }
        }

        return owners;
    }

    private static Dictionary<ulong, ulong> GetBuilders(IReadOnlyList<IteratorPlanNode> planNodes)
    {
        var builders = new Dictionary<ulong, ulong>();

        foreach (var planNode in planNodes)
        {
            builders.TryAdd(planNode.PlanObject, planNode.Iterator);

            if (planNode.BatchPlanObject != 0)
            {
                builders.TryAdd(planNode.BatchPlanObject, planNode.Iterator);
            }
        }

        return builders;
    }

    private static List<SweepSpan<TracePosition>> GetCalls(TraceCallStore calls,
                                                           IReadOnlySet<ulong> iterators,
                                                           Dictionary<ulong, ulong> builders)
    {
        var owned = new List<(TraceCallList List, ulong Owner)>();

        var capacity = 0;

        foreach (var list in calls.GetCallLists())
        {
            var instanceAddress = list.InstanceAddress;

            var owner = instanceAddress == 0
                ? 0
                : iterators.Contains(instanceAddress) ? instanceAddress : builders.GetValueOrDefault(instanceAddress);

            if (owner != 0 || (instanceAddress == 0 && IteratorSymbolLookup.IsIteratorConstructor(calls.GetFirstNode(list)?.Frame)))
            {
                owned.Add((list, owner));

                capacity += list.Count;
            }
        }

        var spans = new List<SweepSpan<TracePosition>>(capacity);

        var buffer = new TraceCallStore.ColumnBuffer();

        foreach (var (list, owner) in owned)
        {
            foreach (var (columns, _) in list.ReadChunks(buffer))
            {
                for (var callIndex = 0; callIndex < columns.Calls; callIndex++)
                {
                    var iterator = owner != 0 ? owner : columns.GetRegister(callIndex, InstanceSlot);

                    if (owner == 0 && !iterators.Contains(iterator))
                    {
                        continue;
                    }

                    spans.Add(new SweepSpan<TracePosition>(columns.GetThread(callIndex),
                                                           columns.GetFromPosition(callIndex),
                                                           columns.GetToPosition(callIndex),
                                                           iterator));
                }
            }
        }

        return spans;
    }
}
