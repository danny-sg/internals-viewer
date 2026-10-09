using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public static class TimeTravelMemory
{
    public static (ulong Bytes, long Allocations) Apply(TimeTravelCallLog log,
                                                         IReadOnlyList<MemoryFunction> functions,
                                                         TimeTravelTimeline? timeline)
    {
        var allocators = functions.Where(f => f.Allocates).ToDictionary(f => f.Address);

        var sizes = new Dictionary<ulong, ulong[]>();

        var totals = new Dictionary<CallStackNode, (ulong Bytes, long Count)>(ReferenceEqualityComparer.Instance);

        foreach (var (address, function) in allocators)
        {
            if (log.CallsOf(address, 0) is not { } calls)
            {
                continue;
            }

            var bytes = new ulong[calls.Count];

            for (var index = 0; index < calls.Count; index++)
            {
                var call = calls[index];

                bytes[index] = function.BytesOf(call);

                if (log.NodeOf(call) is { } node)
                {
                    totals[node] = totals.TryGetValue(node, out var total)
                        ? (total.Bytes + bytes[index], total.Count + 1)
                        : (bytes[index], 1);
                }
            }

            sizes[address] = bytes;
        }

        ulong allocated = 0;

        long allocations = 0;

        foreach (var (node, (bytes, count)) in totals)
        {
            if (node.Ancestors().Skip(1).Any(a => a.Frame is { } frame && allocators.ContainsKey(frame.Address)))
            {
                continue;
            }

            node.AllocatedBytes += (long)bytes;

            node.Allocations += count;

            allocated += bytes;

            allocations += count;
        }

        timeline?.SetAllocations(AllocationsOf(timeline, sizes));

        return (allocated, allocations);
    }

    private static List<TimeTravelAllocation> AllocationsOf(TimeTravelTimeline timeline, Dictionary<ulong, ulong[]> sizes)
    {
        var allocations = new List<TimeTravelAllocation>();

        foreach (var thread in timeline.Threads)
        {
            foreach (var row in thread.Rows)
            {
                var starts = row.Starts(TimeTravelTimelineAxis.Position);

                var ends = row.Ends(TimeTravelTimelineAxis.Position);

                for (var index = 0; index < row.Count; index++)
                {
                    if (timeline.NodeOf(row.NodeAt(index))?.Frame is not { } frame
                        || !sizes.TryGetValue(frame.Address, out var bytes)
                        || row.CallAt(index) is not (>= 0 and var call)
                        || call >= bytes.Length)
                    {
                        continue;
                    }

                    allocations.Add(new TimeTravelAllocation(thread.ThreadId, starts[index], ends[index], bytes[call]));
                }
            }
        }

        return allocations;
    }
}
