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
                                    IReadOnlyDictionary<ulong, TracePosition> firstPositions,
                                    IReadOnlyList<IteratorPlanNode> planNodes)
    {
        var owners = EnclosingSweep.GetEnclosing(GetCalls(calls, iterators, GetBuilders(planNodes)),
                                                 [.. blocks.Select(b => new SweepSpan<TracePosition>(b.Thread, b.FromPosition, b.FromPosition, 0))]);

        var iteratorBlocks = GetIteratorBlocks(blocks, iterators, firstPositions);

        for (var index = 0; index < blocks.Count; index++)
        {
            var address = blocks[index].Address;

            if (iteratorBlocks.GetValueOrDefault(address, -1) == index)
            {
                owners[index] = address;
            }
            else if (owners[index] == address)
            {
                owners[index] = 0;
            }
        }

        return owners;
    }

    private static Dictionary<ulong, int> GetIteratorBlocks(IReadOnlyList<MemoryBlock> blocks,
                                                            IReadOnlySet<ulong> iterators,
                                                            IReadOnlyDictionary<ulong, TracePosition> firstPositions)
    {
        var found = new Dictionary<ulong, int>();

        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];

            if (iterators.Contains(block.Address)
                && block.FromPosition <= firstPositions.GetValueOrDefault(block.Address, TracePosition.Unknown)
                && (!found.TryGetValue(block.Address, out var current) || blocks[current].FromPosition < block.FromPosition))
            {
                found[block.Address] = index;
            }
        }

        return found;
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
                for (var callIndex = 0; callIndex < columns.CallCount; callIndex++)
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
