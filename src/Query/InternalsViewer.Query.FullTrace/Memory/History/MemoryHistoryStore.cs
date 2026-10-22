using System.Runtime.CompilerServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed partial class MemoryHistoryStore(MemoryHistoryBlock[] blocks,
                                               MemoryHistoryWrite[] writes,
                                               MemoryHistoryChange[] changes,
                                               byte[] data,
                                               IReadOnlyDictionary<int, ulong> iterators)
{
    public static MemoryHistoryStore Empty { get; } = new([], [], [], [], new Dictionary<int, ulong>());

    public IReadOnlyList<MemoryHistoryBlock> Blocks { get; } = blocks;

    public int WriteCount => Writes.Length;

    public int ChangeCount => Changes.Length;

    internal long Bytes => Writes.Length * (long)Unsafe.SizeOf<MemoryHistoryWrite>()
                           + Changes.Length * (long)Unsafe.SizeOf<MemoryHistoryChange>()
                           + Data.Length;

    private MemoryHistoryBlock[] BlockArray { get; } = blocks;

    private MemoryHistoryWrite[] Writes { get; } = writes;

    private MemoryHistoryChange[] Changes { get; } = changes;

    private byte[] Data { get; } = data;

    private IReadOnlyDictionary<int, ulong> Iterators { get; } = iterators;

    private AddressIndex Addresses => field ??= GetAddressIndex();


    public IReadOnlyList<MemoryHistoryWrite> GetWrites(int block)
        => block >= 0 && block < BlockArray.Length
            ? new ArraySegment<MemoryHistoryWrite>(Writes, BlockArray[block].FirstWrite, BlockArray[block].WriteCount)
            : ArraySegment<MemoryHistoryWrite>.Empty;

    public ulong GetIterator(MemoryHistoryWrite write) => Iterators.GetValueOrDefault(write.CallNodeIndex);

    public ReadOnlySpan<MemoryHistoryChange> GetChanges(MemoryHistoryWrite write)
        => Changes.AsSpan(write.FirstChange, write.ChangeCount);

    public ReadOnlySpan<byte> GetBytes(MemoryHistoryChange change) => Data.AsSpan(change.DataIndex, change.Length);

    public int GetByteCount(MemoryHistoryWrite write, MemoryHistoryChangeType type)
    {
        var count = 0;

        foreach (var change in GetChanges(write))
        {
            if (change.Type == type)
            {
                count += change.Length;
            }
        }

        return count;
    }

    public MemoryHistoryImage GetImage(int block, int writeIndex)
    {
        var size = BlockArray[block].Size;

        var data = new byte[size];

        var known = new bool[size];

        var changed = new bool[size];

        var writes = GetBlockWrites(block);

        for (var index = 0; index < writes.Length && index <= writeIndex; index++)
        {
            foreach (var change in GetChanges(writes[index]))
            {
                GetBytes(change).CopyTo(data.AsSpan(change.Offset));

                known.AsSpan(change.Offset, change.Length).Fill(true);

                if (change.Type == MemoryHistoryChangeType.Changed)
                {
                    changed.AsSpan(change.Offset, change.Length).Fill(true);
                }
            }
        }

        return new MemoryHistoryImage(data, known, changed);
    }

    public int GetWriteCount(int block, TracePosition position)
        => SortedSearch.FirstAfter<MemoryHistoryWrite, TracePosition>(GetBlockWrites(block), position, w => w.Position);

    public int GetBlock(ulong address, TracePosition position)
    {
        var (blocks, starts, largest) = Addresses;

        var found = -1;

        for (var index = SortedSearch.FirstAfter(starts, address) - 1; index >= 0 && address - starts[index] < largest; index--)
        {
            var block = blocks[index];

            if (IsLiveAt(block, address, position) && (found < 0 || BlockArray[block].Size < BlockArray[found].Size))
            {
                found = block;
            }
        }

        return found;
    }

    public bool IsLive(int block, TracePosition position)
        => BlockArray[block].FromPosition <= position && position < BlockArray[block].ToPosition;

    public bool[] GetChangingBytes(int block)
    {
        var changing = new bool[BlockArray[block].Size];

        foreach (var write in GetBlockWrites(block))
        {
            foreach (var change in GetChanges(write))
            {
                if (change.Type == MemoryHistoryChangeType.Changed)
                {
                    changing.AsSpan(change.Offset, change.Length).Fill(true);
                }
            }
        }

        return changing;
    }

    public IReadOnlyList<MemoryHistoryCall> GetCalls(int block, TraceTimeline timeline, TracePosition from, TracePosition to)
    {
        if (block < 0 || block >= BlockArray.Length)
        {
            return [];
        }

        var calls = new Dictionary<(int CallNodeIndex, int CallIndex), CallBuilder>();

        var ordered = new List<CallBuilder>();

        var writes = GetBlockWrites(block);

        var image = new byte[BlockArray[block].Size];

        var firstListed = -1;

        for (var index = 0; index < writes.Length; index++)
        {
            var write = writes[index];

            var changes = GetChanges(write);

            if (IsChange(write, from, to))
            {
                if (firstListed < 0)
                {
                    firstListed = index;
                }

                if (!calls.TryGetValue((write.CallNodeIndex, write.CallIndex), out var call))
                {
                    call = new CallBuilder(timeline.NodeOf(write.CallNodeIndex), write, GetIterator(write));

                    calls[(write.CallNodeIndex, write.CallIndex)] = call;

                    ordered.Add(call);
                }

                call.Add(index, changes, Data, image);
            }

            foreach (var change in changes)
            {
                GetBytes(change).CopyTo(image.AsSpan(change.Offset));
            }
        }

        if (firstListed < 0)
        {
            return [];
        }

        return [GetAllocationCall(block, timeline, firstListed), .. ordered.Select(c => c.Build()).OrderBy(c => c.Position)];
    }

    public MemoryHistorySummary GetSummary(int block, TracePosition from, TracePosition to) => GetSummaries([block], from, to)[0];

    public MemoryHistorySummary[] GetSummaries(int[] blocks, TracePosition from, TracePosition to)
    {
        var summaries = new MemoryHistorySummary[blocks.Length];

        var calls = new HashSet<(int CallNodeIndex, int CallIndex)>();

        var changing = Array.Empty<bool>();

        for (var index = 0; index < blocks.Length; index++)
        {
            var size = BlockArray[blocks[index]].Size;

            if (changing.Length < size)
            {
                changing = new bool[size];
            }

            changing.AsSpan(0, size).Clear();

            calls.Clear();

            summaries[index] = GetSummary(blocks[index], from, to, calls, changing.AsSpan(0, size));
        }

        return summaries;
    }

    public MemoryHistoryWrite GetWrite(int index) => Writes[index];

    public int[] GetChangedWriteIndexes(int block, TracePosition from, TracePosition to)
    {
        var writes = GetBlockWrites(block);

        var count = 0;

        foreach (var write in writes)
        {
            count += IsChange(write, from, to) ? 1 : 0;
        }

        var indexes = new int[count];

        var at = 0;

        for (var index = 0; index < writes.Length; index++)
        {
            if (IsChange(writes[index], from, to))
            {
                indexes[at] = BlockArray[block].FirstWrite + index;

                at++;
            }
        }

        return indexes;
    }

    public (int Offset, int Length) GetChangedExtent(MemoryHistoryWrite write)
    {
        var start = int.MaxValue;

        var end = 0;

        foreach (var change in GetChanges(write))
        {
            if (change.Type == MemoryHistoryChangeType.Changed)
            {
                start = Math.Min(start, change.Offset);

                end = Math.Max(end, change.Offset + change.Length);
            }
        }

        return (start, end - start);
    }

    private MemoryHistorySummary GetSummary(int block,
                                            TracePosition from,
                                            TracePosition to,
                                            HashSet<(int CallNodeIndex, int CallIndex)> calls,
                                            Span<bool> changing)
    {
        var operators = new Dictionary<ulong, int>();

        var changeCount = 0;

        var lastCall = (CallNodeIndex: int.MinValue, CallIndex: int.MinValue);

        var (iterator, iteratorCount) = (0ul, 0);

        foreach (var write in GetBlockWrites(block))
        {
            if (write.Position < from || write.Position > to || !MarkChanged(write, changing))
            {
                continue;
            }

            changeCount++;

            if ((write.CallNodeIndex, write.CallIndex) == lastCall)
            {
                iteratorCount++;

                continue;
            }

            lastCall = (write.CallNodeIndex, write.CallIndex);

            calls.Add(lastCall);

            AddOperator(operators, iterator, iteratorCount);

            (iterator, iteratorCount) = (GetIterator(write), 1);
        }

        AddOperator(operators, iterator, iteratorCount);

        return new MemoryHistorySummary([.. operators.OrderByDescending(o => o.Value).Select(o => o.Key)],
                                        calls.Count,
                                        changeCount,
                                        changing.Count(true));
    }

    public TracePosition GetCreatedPosition(int block)
        => BlockArray[block] is { IsInline: true, WriteCount: > 0 } inline ? Writes[inline.FirstWrite].Position : BlockArray[block].FromPosition;

    public bool IsCreatedWithin(int block, TracePosition from, TracePosition to)
        => GetCreatedPosition(block) is var created && from <= created && created <= to;

    public CallStackNode? GetAllocatingCall(int block, TraceTimeline timeline)
        => timeline.NodeOf(BlockArray[block].CallNodeIndex)?.Ancestors().FirstOrDefault(n => !MemoryFunction.IsAllocatorFrame(n.Frame));

    private MemoryHistoryCall GetAllocationCall(int block, TraceTimeline timeline, int firstListed)
    {
        var allocation = BlockArray[block];

        var writes = GetBlockWrites(block);

        var first = writes[0];

        var node = allocation.IsInline ? timeline.NodeOf(first.CallNodeIndex) : GetAllocatingCall(block, timeline);

        var regions = new List<MemoryHistoryRegion>();

        for (var index = 0; index < firstListed; index++)
        {
            foreach (var change in GetChanges(writes[index]))
            {
                regions.Add(new MemoryHistoryRegion(change.Offset, change.Length));
            }
        }

        var initialised = Merge(regions);

        var (callIndex, position) = allocation.IsInline
            ? (first.CallIndex, first.Position)
            : (node is null ? -1 : GetCallIndex(timeline, node, allocation.Thread, allocation.FromPosition), allocation.FromPosition);

        return new MemoryHistoryCall(node,
                                     allocation.Iterator,
                                     callIndex,
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

    private ReadOnlySpan<MemoryHistoryWrite> GetBlockWrites(int block)
        => Writes.AsSpan(BlockArray[block].FirstWrite, BlockArray[block].WriteCount);

    private bool IsLiveAt(int block, ulong address, TracePosition position)
        => address < BlockArray[block].Address + (ulong)BlockArray[block].Size && IsLive(block, position);

    private bool MarkChanged(MemoryHistoryWrite write, Span<bool> changing)
    {
        var isChanged = false;

        foreach (var change in GetChanges(write))
        {
            if (change.Type == MemoryHistoryChangeType.Changed)
            {
                changing.Slice(change.Offset, change.Length).Fill(true);

                isChanged = true;
            }
        }

        return isChanged;
    }

    private static void AddOperator(Dictionary<ulong, int> operators, ulong iterator, int count)
    {
        if (iterator != 0 && count > 0)
        {
            operators[iterator] = operators.GetValueOrDefault(iterator) + count;
        }
    }

    private bool IsChange(MemoryHistoryWrite write, TracePosition from, TracePosition to)
        => from <= write.Position && write.Position <= to && GetByteCount(write, MemoryHistoryChangeType.Changed) > 0;

    private static void AddChanged(List<MemoryHistoryRegion> regions, ReadOnlySpan<MemoryHistoryChange> changes)
    {
        foreach (var change in changes)
        {
            if (change.Type == MemoryHistoryChangeType.Changed)
            {
                regions.Add(new MemoryHistoryRegion(change.Offset, change.Length));
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

    private AddressIndex GetAddressIndex()
    {
        int[] blocks = [.. Enumerable.Range(0, BlockArray.Length).OrderBy(b => BlockArray[b].Address)];

        return new AddressIndex(blocks,
                                [.. blocks.Select(b => BlockArray[b].Address)],
                                BlockArray.Length == 0 ? 0 : (ulong)BlockArray.Max(b => b.Size));
    }

    private sealed record AddressIndex(int[] Blocks, ulong[] Starts, ulong Largest);

    private sealed class CallBuilder(CallStackNode? node, MemoryHistoryWrite first, ulong iterator)
    {
        private List<MemoryHistoryRegion> Changed { get; } = [];

        private Dictionary<int, (byte Before, byte After)> Values { get; } = [];

        private int ChangeCount { get; set; }

        private int LastWrite { get; set; }

        public void Add(int writeIndex, ReadOnlySpan<MemoryHistoryChange> changes, byte[] data, byte[] image)
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

                for (var index = 0; index < change.Length; index++)
                {
                    var offset = change.Offset + index;

                    var before = Values.TryGetValue(offset, out var value) ? value.Before : image[offset];

                    Values[offset] = (before, data[change.DataIndex + index]);
                }
            }
        }

        public MemoryHistoryCall Build()
            => new(node,
                   iterator,
                   first.CallIndex,
                   first.Position,
                   LastWrite,
                   ChangeCount,
                   Merge(Changed),
                   Merge([.. Values.Where(v => v.Value.Before != v.Value.After).Select(v => new MemoryHistoryRegion(v.Key, 1))]));
    }
}
