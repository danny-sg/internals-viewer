using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed class AllocationMap
{
    private const ulong SmallestAddress = 0x10000;

    private AllocationMap(Dictionary<ulong, TraceAllocation[]> allocations, IReadOnlyList<AllocationCall> calls)
    {
        Allocations = allocations;
        Calls = calls;
    }

    public static AllocationMap Empty { get; } = new([], []);

    public int Count => Allocations.Values.Sum(a => a.Length);

    internal IReadOnlyList<AllocationCall> Calls { get; }

    private Dictionary<ulong, TraceAllocation[]> Allocations { get; }

    public bool Contains(ulong address) => Allocations.ContainsKey(address);

    public TraceAllocation? GetAllocation(ulong address, double position)
    {
        if (!Allocations.TryGetValue(address, out var allocations))
        {
            return null;
        }

        foreach (var candidate in allocations)
        {
            if (candidate.First <= position && position <= candidate.Last)
            {
                return candidate;
            }
        }

        return null;
    }

    internal static AllocationMap Build(TraceCallLog callLog, TraceTimeline timeline, ObjectFunctions functions)
    {
        if (functions.Allocators.Count == 0)
        {
            return Empty;
        }

        var reader = new CallReader(callLog, timeline);

        var calls = new List<AllocationCall>();

        var frees = new Dictionary<ulong, List<double>>();

        var positioned = new HashSet<(ulong Address, ulong Instance, int Call)>();

        foreach (var thread in timeline.Threads)
        {
            for (var depth = 0; depth < thread.Rows.Count; depth++)
            {
                var row = thread.Rows[depth];

                for (var index = 0; index < row.Count; index++)
                {
                    if (timeline.NodeOf(row.NodeAt(index))?.Frame is not { } frame || row.CallAt(index) is not (>= 0 and var call))
                    {
                        continue;
                    }

                    if (functions.Allocators.Contains(frame.Address))
                    {
                        positioned.Add((frame.Address, frame.Instance, call));

                        if (reader.Get(row, index) is { Returned: true, ReturnValue: >= SmallestAddress } allocated)
                        {
                            calls.Add(GetAllocationCall(timeline, thread, depth, index, allocated.ReturnValue, reader));
                        }
                    }
                    else if (functions.Frees.TryGetValue(frame.Address, out var slot))
                    {
                        positioned.Add((frame.Address, frame.Instance, call));

                        if (reader.Get(row, index)?.Slot(slot) is >= SmallestAddress and var freed)
                        {
                            Add(frees, freed, row.Starts(TraceTimelineAxis.Position)[index]);
                        }
                    }
                }
            }
        }

        AddUnpositioned(callLog, timeline, functions, positioned, calls, frees);

        var allocations = new Dictionary<ulong, TraceAllocation[]>();

        foreach (var group in calls.DistinctBy(c => (c.Pointer, c.Start)).GroupBy(c => c.Pointer))
        {
            allocations[group.Key] = GetAllocations(group.Key, [.. group.OrderBy(c => c.Start)], frees.GetValueOrDefault(group.Key));
        }

        return new AllocationMap(allocations, calls);
    }

    private static AllocationCall GetAllocationCall(TraceTimeline timeline,
                                                    TraceTimelineThread thread,
                                                    int depth,
                                                    int index,
                                                    ulong pointer,
                                                    CallReader reader)
    {
        var row = thread.Rows[depth];

        var start = row.Starts(TraceTimelineAxis.Position)[index];

        var end = row.Ends(TraceTimelineAxis.Position)[index];

        var allocator = GetSymbol(timeline, row.NodeAt(index));

        for (; depth > 0 && GetParent(thread.Rows[depth - 1], start, end) is { } parent; depth--)
        {
            var parentRow = thread.Rows[depth - 1];

            if (reader.Get(parentRow, parent) is not { Returned: true } caller || caller.ReturnValue != pointer)
            {
                return new AllocationCall(pointer, start, end, parentRow.Ends(TraceTimelineAxis.Position)[parent], allocator);
            }

            start = parentRow.Starts(TraceTimelineAxis.Position)[parent];

            end = parentRow.Ends(TraceTimelineAxis.Position)[parent];

            allocator = GetSymbol(timeline, parentRow.NodeAt(parent));
        }

        return new AllocationCall(pointer, start, end, end, allocator);
    }

    private static void AddUnpositioned(TraceCallLog callLog,
                                        TraceTimeline timeline,
                                        ObjectFunctions functions,
                                        HashSet<(ulong Address, ulong Instance, int Call)> positioned,
                                        List<AllocationCall> calls,
                                        Dictionary<ulong, List<double>> frees)
    {
        foreach (var (address, instance) in callLog.FunctionKeys)
        {
            var allocates = functions.Allocators.Contains(address);

            var slot = functions.Frees.GetValueOrDefault(address, -1);

            if ((!allocates && slot < 0) || callLog.CallsOf(address, instance) is not { } list)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                if (positioned.Contains((address, instance, index)))
                {
                    continue;
                }

                var call = list[index];

                if (allocates && call is { Returned: true, ReturnValue: >= SmallestAddress })
                {
                    calls.Add(new AllocationCall(call.ReturnValue,
                                                 call.Sequence,
                                                 call.Sequence,
                                                 call.Sequence,
                                                 GetSymbol(timeline, call.Node)));
                }
                else if (!allocates && call.Slot(slot) is >= SmallestAddress and var freed)
                {
                    Add(frees, freed, call.Sequence);
                }
            }
        }
    }

    private static TraceAllocation[] GetAllocations(ulong pointer, AllocationCall[] calls, List<double>? frees)
    {
        frees?.Sort();

        var allocations = new TraceAllocation[calls.Length];

        for (var index = 0; index < calls.Length; index++)
        {
            var call = calls[index];

            var next = index + 1 < calls.Length ? calls[index + 1].Start : double.PositiveInfinity;

            allocations[index] = new TraceAllocation(pointer, call.Allocator ?? "Allocation", call.End, GetFreed(frees, call.End, next));
        }

        return allocations;
    }

    private static string? GetSymbol(TraceTimeline timeline, int node)
        => timeline.NodeOf(node)?.Symbol is { Length: > 0 } symbol ? symbol : null;

    private static double GetFreed(List<double>? frees, double after, double before)
    {
        foreach (var free in frees ?? [])
        {
            if (free >= after && free < before)
            {
                return free;
            }
        }

        return before;
    }

    private static int? GetParent(TraceTimelineRow row, double start, double end)
    {
        var index = row.FirstStartingAfter(TraceTimelineAxis.Position, start) - 1;

        return index >= 0 && row.Ends(TraceTimelineAxis.Position)[index] >= end ? index : null;
    }

    private static void Add(Dictionary<ulong, List<double>> frees, ulong pointer, double position)
    {
        if (!frees.TryGetValue(pointer, out var list))
        {
            list = [];

            frees[pointer] = list;
        }

        list.Add(position);
    }

    private sealed class CallReader(TraceCallLog callLog, TraceTimeline timeline)
    {
        private Dictionary<(ulong Address, ulong Instance), TraceCallList?> Lists { get; } = [];

        public TraceArgumentCall? Get(TraceTimelineRow row, int index)
        {
            if (timeline.NodeOf(row.NodeAt(index))?.Frame is not { } frame || row.CallAt(index) is not (>= 0 and var call))
            {
                return null;
            }

            var key = (frame.Address, frame.Instance);

            if (!Lists.TryGetValue(key, out var calls))
            {
                calls = callLog.CallsOf(frame.Address, frame.Instance);

                Lists[key] = calls;
            }

            return calls is not null && call < calls.Count ? calls[call] : null;
        }
    }
}
