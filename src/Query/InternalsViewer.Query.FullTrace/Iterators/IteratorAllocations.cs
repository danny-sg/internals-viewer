using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal sealed class IteratorAllocations
{
    private const int InstanceSlot = 0;

    private IteratorAllocations(MemoryBlock[] blocks, ulong[] owners, IReadOnlySet<ulong> iterators)
    {
        Blocks = blocks;

        Pointers = [.. blocks.Select(b => b.Pointer)];

        Owners = owners;

        Iterators = iterators;

        MaximumBytes = blocks.Length > 0 ? blocks.Max(b => b.Bytes) : 0;
    }

    public int Count => Blocks.Length;

    private MemoryBlock[] Blocks { get; }

    private ulong[] Pointers { get; }

    private ulong[] Owners { get; }

    private IReadOnlySet<ulong> Iterators { get; }

    private ulong MaximumBytes { get; }

    public static IteratorAllocations Build(TraceTimeline timeline,
                                            MemoryIndex memory,
                                            TraceCallStore calls,
                                            IReadOnlySet<ulong> iterators,
                                            IReadOnlyList<IteratorPlanNode> planNodes)
    {
        MemoryBlock[] blocks = [.. memory.Blocks.OrderBy(b => b.Pointer)];

        var owners = EnclosingSweep.GetEnclosing(GetCalls(timeline, calls, iterators, GetBuilders(planNodes)),
                                                 [.. blocks.Select(b => new SweepSpan<double>(b.Thread, b.Start, b.Start, 0))]);

        for (var index = 0; index < blocks.Length; index++)
        {
            if (iterators.Contains(blocks[index].Pointer))
            {
                owners[index] = blocks[index].Pointer;
            }
        }

        return new IteratorAllocations(blocks, owners, iterators);
    }

    public ulong GetOwner(ulong address, double position)
    {
        if (address == 0)
        {
            return 0;
        }

        if (Iterators.Contains(address))
        {
            return address;
        }

        for (var index = SortedSearch.FirstAtOrAfter(Pointers, address + 1) - 1;
             index >= 0 && address - Pointers[index] < MaximumBytes;
             index--)
        {
            if (Blocks[index].Contains(address, position))
            {
                return Owners[index];
            }
        }

        return 0;
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

    private static List<SweepSpan<double>> GetCalls(TraceTimeline timeline,
                                                    TraceCallStore calls,
                                                    IReadOnlySet<ulong> iterators,
                                                    Dictionary<ulong, ulong> builders)
    {
        var spans = new List<SweepSpan<double>>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (calls.GetCallList(address, instance) is not { Count: > 0 } list)
            {
                continue;
            }

            if (instance != 0)
            {
                var owner = iterators.Contains(instance) ? instance : builders.GetValueOrDefault(instance);

                if (owner == 0)
                {
                    continue;
                }

                for (var index = 0; index < list.Count; index++)
                {
                    spans.Add(GetSpan(timeline, list, index, owner));
                }
            }
            else if (IsConstructor(calls.GetNode(list.GetNodeIndex(0))?.Frame))
            {
                for (var index = 0; index < list.Count; index++)
                {
                    if (list[index].IntegerSlots is { Length: > InstanceSlot } slots && iterators.Contains(slots[InstanceSlot]))
                    {
                        spans.Add(GetSpan(timeline, list, index, slots[InstanceSlot]));
                    }
                }
            }
        }

        return spans;
    }

    private static SweepSpan<double> GetSpan(TraceTimeline timeline, TraceCallList list, int index, ulong iterator)
    {
        var to = list.GetToPosition(index);

        return new SweepSpan<double>(list.GetThread(index),
                                     timeline.PositionOf(list.GetFromPosition(index)),
                                     to.IsKnown ? timeline.PositionOf(to) : double.PositiveInfinity,
                                     iterator);
    }

    private static bool IsConstructor(CallstackFrame? frame)
        => frame?.Resolved is { ClassName: { } className } resolved
           && IteratorSymbols.IsIterator(className)
           && resolved.MethodName == className;
}
