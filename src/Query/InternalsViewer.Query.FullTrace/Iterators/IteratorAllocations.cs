using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal sealed class IteratorAllocations
{
    private const string IteratorPrefix = "CQScan";

    private const string BatchIteratorPrefix = "CBpQScan";

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
                                            TraceCallStore calls,
                                            IReadOnlySet<ulong> iterators,
                                            IReadOnlyList<IteratorPlanNode> planNodes)
    {
        MemoryBlock[] blocks = [.. timeline.Memory.Blocks.OrderBy(b => b.Pointer)];

        var owners = new ulong[blocks.Length];

        var threads = GetCalls(timeline, calls, iterators, GetBuilders(planNodes));

        for (var index = 0; index < blocks.Length; index++)
        {
            if (iterators.Contains(blocks[index].Pointer))
            {
                owners[index] = blocks[index].Pointer;

                continue;
            }

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, blocks[index].Thread, out _);

            (list ??= []).Add(new SweepEntry(blocks[index].Start, blocks[index].Start, 0, index));
        }

        var open = new Stack<SweepEntry>();

        foreach (var thread in threads.Values)
        {
            thread.Sort((left, right) => left.From != right.From ? left.From.CompareTo(right.From) : right.To.CompareTo(left.To));

            open.Clear();

            foreach (var entry in thread)
            {
                while (open.TryPeek(out var outer) && outer.To <= entry.From)
                {
                    open.Pop();
                }

                if (entry.Block >= 0)
                {
                    owners[entry.Block] = open.TryPeek(out var enclosing) ? enclosing.Iterator : 0;

                    continue;
                }

                open.Push(entry);
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

    private static Dictionary<uint, List<SweepEntry>> GetCalls(TraceTimeline timeline,
                                                               TraceCallStore calls,
                                                               IReadOnlySet<ulong> iterators,
                                                               Dictionary<ulong, ulong> builders)
    {
        var threads = new Dictionary<uint, List<SweepEntry>>();

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
                    Add(list.GetThread(index), Span(timeline, list, index, owner));
                }
            }
            else if (IsConstructor(calls.GetNode(list.GetNodeIndex(0))?.Frame))
            {
                for (var index = 0; index < list.Count; index++)
                {
                    if (list[index].IntegerSlots is { Length: > InstanceSlot } slots && iterators.Contains(slots[InstanceSlot]))
                    {
                        Add(list.GetThread(index), Span(timeline, list, index, slots[InstanceSlot]));
                    }
                }
            }
        }

        return threads;

        void Add(uint thread, SweepEntry entry)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, thread, out _);

            (list ??= []).Add(entry);
        }
    }

    private static SweepEntry Span(TraceTimeline timeline, TraceCallList list, int index, ulong iterator)
    {
        var to = list.GetToPosition(index);

        return new SweepEntry(timeline.PositionOf(list.GetFromPosition(index)),
                              to.IsKnown ? timeline.PositionOf(to) : double.PositiveInfinity,
                              iterator,
                              -1);
    }

    private static bool IsConstructor(CallstackFrame? frame)
        => frame?.Resolved is { ClassName: { } className } resolved
           && (className.StartsWith(IteratorPrefix, StringComparison.Ordinal) || className.StartsWith(BatchIteratorPrefix, StringComparison.Ordinal))
           && resolved.MethodName == className;

    private readonly record struct SweepEntry(double From, double To, ulong Iterator, int Block);
}
