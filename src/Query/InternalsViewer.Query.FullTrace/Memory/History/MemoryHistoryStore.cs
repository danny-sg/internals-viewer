using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed partial class MemoryHistoryStore(MemoryHistoryBlock[] blocks, MemoryHistoryWrite[] writes, MemoryHistoryChange[] changes)
{
    public static MemoryHistoryStore Empty { get; } = new([], [], []);

    public IReadOnlyList<MemoryHistoryBlock> Blocks { get; } = blocks;

    public int WriteCount => Writes.Length;

    public int ChangeCount => Changes.Length;

    private MemoryHistoryBlock[] BlockArray { get; } = blocks;

    private MemoryHistoryWrite[] Writes { get; } = writes;

    private MemoryHistoryChange[] Changes { get; } = changes;

    private int[][] WritesByBlock => field ??= GetWritesByBlock();

    public IReadOnlyList<MemoryHistoryWrite> GetWrites(int block)
        => block >= 0 && block < WritesByBlock.Length ? [.. WritesByBlock[block].Select(m => Writes[m])] : [];

    public ReadOnlySpan<MemoryHistoryChange> GetChanges(MemoryHistoryWrite write)
        => Changes.AsSpan(write.FirstChange, write.ChangeCount);

    public int GetByteCount(MemoryHistoryWrite write, MemoryHistoryChangeType type)
    {
        var count = 0;

        foreach (var change in GetChanges(write))
        {
            if (change.Type == type)
            {
                count += change.Bytes.Length;
            }
        }

        return count;
    }

    public MemoryHistoryImage GetImage(int block, int writeIndex)
    {
        var size = BlockArray[block].Size;

        var data = new byte[size];

        var known = new bool[size];

        var writes = WritesByBlock[block];

        for (var index = 0; index < writes.Length && index <= writeIndex; index++)
        {
            foreach (var change in GetChanges(Writes[writes[index]]))
            {
                change.Bytes.CopyTo(data.AsSpan(change.Offset));

                known.AsSpan(change.Offset, change.Bytes.Length).Fill(true);
            }
        }

        return new MemoryHistoryImage(data, known);
    }

    public bool[] GetChangingBytes(int block)
    {
        var changing = new bool[BlockArray[block].Size];

        foreach (var write in WritesByBlock[block])
        {
            foreach (var change in GetChanges(Writes[write]))
            {
                if (change.Type == MemoryHistoryChangeType.Changed)
                {
                    changing.AsSpan(change.Offset, change.Bytes.Length).Fill(true);
                }
            }
        }

        return changing;
    }

    public IReadOnlyList<MemoryHistoryCall> GetCalls(int block, TraceTimeline timeline, TracePosition from, TracePosition to)
    {
        if (block < 0 || block >= WritesByBlock.Length)
        {
            return [];
        }

        var calls = new Dictionary<(int CallNodeIndex, int CallIndex), CallBuilder>();

        var ordered = new List<CallBuilder>();

        var writes = WritesByBlock[block];

        var image = new byte[BlockArray[block].Size];

        var firstListed = -1;

        for (var index = 0; index < writes.Length; index++)
        {
            var write = Writes[writes[index]];

            var changes = GetChanges(write);

            if (IsChange(write, from, to))
            {
                if (firstListed < 0)
                {
                    firstListed = index;
                }

                if (!calls.TryGetValue((write.CallNodeIndex, write.CallIndex), out var call))
                {
                    call = new CallBuilder(timeline.NodeOf(write.CallNodeIndex), write);

                    calls[(write.CallNodeIndex, write.CallIndex)] = call;

                    ordered.Add(call);
                }

                call.Add(index, changes, image);
            }

            foreach (var change in changes)
            {
                change.Bytes.CopyTo(image.AsSpan(change.Offset));
            }
        }

        if (firstListed < 0)
        {
            return [];
        }

        return [GetAllocationCall(block, timeline, firstListed), .. ordered.Select(c => c.Build()).OrderBy(c => c.Position)];
    }

    public MemoryHistorySummary GetSummary(int block, TracePosition from, TracePosition to)
    {
        var operators = new Dictionary<ulong, int>();

        var calls = new HashSet<(int CallNodeIndex, int CallIndex)>();

        var regions = new List<MemoryHistoryRegion>();

        var changeCount = 0;

        foreach (var index in WritesByBlock[block])
        {
            var write = Writes[index];

            if (!IsChange(write, from, to))
            {
                continue;
            }

            if (write.Iterator != 0)
            {
                operators[write.Iterator] = operators.GetValueOrDefault(write.Iterator) + 1;
            }

            calls.Add((write.CallNodeIndex, write.CallIndex));

            changeCount++;

            AddChanged(regions, GetChanges(write));
        }

        return new MemoryHistorySummary([.. operators.OrderByDescending(o => o.Value).Select(o => o.Key)],
                                        calls.Count,
                                        changeCount,
                                        Merge(regions).Sum(r => r.Length));
    }

    public IEnumerable<MemoryHistoryWrite> GetChangedWrites(int block, TracePosition from, TracePosition to)
        => WritesByBlock[block].Select(m => Writes[m]).Where(m => IsChange(m, from, to));

    public TracePosition GetCreatedPosition(int block)
        => BlockArray[block].IsInline && WritesByBlock[block].Length > 0
            ? Writes[WritesByBlock[block][0]].Position
            : BlockArray[block].FromPosition;

    public bool IsCreatedWithin(int block, TracePosition from, TracePosition to)
        => GetCreatedPosition(block) is var created && from <= created && created <= to;

    public CallStackNode? GetAllocatingCall(int block, TraceTimeline timeline)
        => timeline.NodeOf(BlockArray[block].CallNodeIndex)?.Ancestors().FirstOrDefault(n => !MemoryFunction.IsAllocatorFrame(n.Frame));

    private MemoryHistoryCall GetAllocationCall(int block, TraceTimeline timeline, int firstListed)
    {
        var allocation = BlockArray[block];

        var writes = WritesByBlock[block];

        var first = Writes[writes[0]];

        var node = allocation.IsInline ? timeline.NodeOf(first.CallNodeIndex) : GetAllocatingCall(block, timeline);

        var regions = new List<MemoryHistoryRegion>();

        for (var index = 0; index < firstListed; index++)
        {
            foreach (var change in GetChanges(Writes[writes[index]]))
            {
                regions.Add(new MemoryHistoryRegion(change.Offset, change.Bytes.Length));
            }
        }

        var initialised = Merge(regions);

        var (callIndex, position) = allocation.IsInline
            ? (first.CallIndex, first.Position)
            : (node is null ? -1 : GetCallIndex(timeline, node, allocation.Thread, allocation.FromPosition), allocation.FromPosition);

        return new MemoryHistoryCall(node,
                                     allocation.Iterator,
                                     callIndex,
                                     allocation.Thread,
                                     position,
                                     firstListed - 1,
                                     firstListed,
                                     initialised,
                                     initialised)
        {
            IsAllocation = true
        };
    }

    private static int GetCallIndex(TraceTimeline timeline, CallStackNode node, uint thread, TracePosition position)
    {
        foreach (var stack in timeline.GetStack(position))
        {
            if (stack.ThreadId != thread)
            {
                continue;
            }

            foreach (var call in stack.Calls)
            {
                if (ReferenceEquals(timeline.NodeOf(call.CallNodeIndex), node))
                {
                    return call.CallIndex;
                }
            }
        }

        return -1;
    }

    private bool IsChange(MemoryHistoryWrite write, TracePosition from, TracePosition to)
        => from <= write.Position && write.Position <= to && GetByteCount(write, MemoryHistoryChangeType.Changed) > 0;

    private static void AddChanged(List<MemoryHistoryRegion> regions, ReadOnlySpan<MemoryHistoryChange> changes)
    {
        foreach (var change in changes)
        {
            if (change.Type == MemoryHistoryChangeType.Changed)
            {
                regions.Add(new MemoryHistoryRegion(change.Offset, change.Bytes.Length));
            }
        }
    }

    private static List<MemoryHistoryRegion> Merge(List<MemoryHistoryRegion> regions)
    {
        var merged = new List<MemoryHistoryRegion>();

        foreach (var region in regions.OrderBy(r => r.Offset))
        {
            if (merged.Count > 0 && merged[^1].Offset + merged[^1].Length >= region.Offset)
            {
                var last = merged[^1];

                var end = Math.Max(last.Offset + last.Length, region.Offset + region.Length);

                merged[^1] = last with { Length = end - last.Offset };

                continue;
            }

            merged.Add(region);
        }

        return merged;
    }

    private int[][] GetWritesByBlock()
    {
        var lists = BlockArray.Select(_ => new List<int>()).ToArray();

        for (var index = 0; index < Writes.Length; index++)
        {
            lists[Writes[index].Block].Add(index);
        }

        return [.. lists.Select(l => l.ToArray())];
    }

    private sealed class CallBuilder(CallStackNode? node, MemoryHistoryWrite first)
    {
        private List<MemoryHistoryRegion> Changed { get; } = [];

        private SortedDictionary<int, (byte Before, byte After)> Values { get; } = [];

        private int ChangeCount { get; set; }

        private int LastWrite { get; set; }

        public void Add(int writeIndex, ReadOnlySpan<MemoryHistoryChange> changes, byte[] image)
        {
            ChangeCount++;

            LastWrite = writeIndex;

            AddChanged(Changed, changes);

            foreach (var change in changes)
            {
                if (change.Type != MemoryHistoryChangeType.Changed)
                {
                    continue;
                }

                for (var index = 0; index < change.Bytes.Length; index++)
                {
                    var offset = change.Offset + index;

                    var before = Values.TryGetValue(offset, out var value) ? value.Before : image[offset];

                    Values[offset] = (before, change.Bytes[index]);
                }
            }
        }

        public MemoryHistoryCall Build()
            => new(node,
                   first.Iterator,
                   first.CallIndex,
                   first.Thread,
                   first.Position,
                   LastWrite,
                   ChangeCount,
                   Merge(Changed),
                   Merge([.. Values.Where(v => v.Value.Before != v.Value.After).Select(v => new MemoryHistoryRegion(v.Key, 1))]));
    }
}
