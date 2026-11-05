using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Buffers.Binary;
using System.Collections;
using System.Runtime.CompilerServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed partial class MemoryHistoryStore
{
    private const int CheckpointShift = 5;

    private const int CheckpointMask = (1 << CheckpointShift) - 1;

    internal MemoryHistoryStore(MemoryHistoryBlock[] blocks,
                                MemoryHistoryWriteEntry[] writes,
                                byte[] data,
                                IReadOnlyDictionary<int, ulong> iterators,
                                TraceTimeline? timeline,
                                MemoryHistoryReadEntry[]? reads = null,
                                int[]? readStarts = null,
                                MemoryHistoryInstance[]? instances = null)
    {
        Blocks = blocks;
        BlockArray = blocks;
        Writes = writes;
        Data = data;
        Iterators = iterators;
        Timeline = timeline;
        Reads = reads ?? [];
        ReadStarts = readStarts ?? [];
        Instances = instances ?? [];
        DataCheckpoints = GetDataCheckpoints(writes);
    }

    public static MemoryHistoryStore Empty { get; } = new([], [], [], new Dictionary<int, ulong>(), null);

    public IReadOnlyList<MemoryHistoryBlock> Blocks { get; }

    public int WriteCount => Writes.Length;

    public int ReadCount => Reads.Length;

    public bool HasReads => ReadStarts.Length > 0;

    internal long Bytes => WriteBytes + ReadBytes + InstanceBytes;

    internal long WriteBytes => Writes.Length * (long)Unsafe.SizeOf<MemoryHistoryWriteEntry>()
                                + DataCheckpoints.Length * (long)sizeof(int)
                                + Data.Length;

    internal long ReadBytes => Reads.Length * (long)Unsafe.SizeOf<MemoryHistoryReadEntry>() + ReadStarts.Length * (long)sizeof(int);

    internal long InstanceBytes => Instances.Length * (long)Unsafe.SizeOf<MemoryHistoryInstance>();

    private MemoryHistoryBlock[] BlockArray { get; }

    private MemoryHistoryWriteEntry[] Writes { get; }

    private byte[] Data { get; }

    private int[] DataCheckpoints { get; }

    private IReadOnlyDictionary<int, ulong> Iterators { get; }

    private TraceTimeline? Timeline { get; }

    private MemoryHistoryReadEntry[] Reads { get; }

    private int[] ReadStarts { get; }

    private MemoryHistoryInstance[] Instances { get; }

    private AddressOrder Addresses => field ??= GetAddressOrder(isStack: false);

    private AddressOrder StackAddresses => field ??= GetAddressOrder(isStack: true);


    public IReadOnlyList<MemoryHistoryWrite> GetWrites(int block)
        => block >= 0 && block < BlockArray.Length
            ? new WriteList(this, BlockArray[block].FirstWrite, BlockArray[block].WriteCount)
            : [];

    public ulong GetIterator(MemoryHistoryWrite write) => Iterators.GetValueOrDefault(write.CallNodeIndex);

    public (int First, int Count) GetReadRange(int block)
        => block >= 0 && block + 1 < ReadStarts.Length ? (ReadStarts[block], ReadStarts[block + 1] - ReadStarts[block]) : (0, 0);

    public IReadOnlyList<MemoryHistoryRead> GetReads(int block)
        => GetReadRange(block) is (var first, > 0 and var count) ? new ReadList(this, first, count) : [];

    public (int First, int Count) GetReadRange(int block, TracePosition from, TracePosition to)
    {
        var (first, count) = GetReadRange(block);

        ReadOnlySpan<MemoryHistoryReadEntry> reads = Reads.AsSpan(first, count);

        var start = SortedSearch.FirstAtOrAfter<MemoryHistoryReadEntry, TracePosition>(reads, from, r => r.Position);

        var end = SortedSearch.FirstAfter<MemoryHistoryReadEntry, TracePosition>(reads, to, r => r.Position, start);

        return (first + start, end - start);
    }

    public MemoryHistoryRead GetRead(int index)
    {
        var read = Reads[index];

        var (callNodeIndex, callIndex) = GetCall(read.SpanId);

        return new MemoryHistoryRead(read.Position, callNodeIndex, callIndex, read.Offset, read.Length, read.Count);
    }

    public TracePoint GetReadPoint(int index) => new(Reads[index].Position, GetInstructions(Reads[index].SpanId, Reads[index].Position));

    public ReadOnlySpan<byte> GetBytes(MemoryHistoryWrite write) => Data.AsSpan(GetDataIndex(write.Index), write.Length);

    public void Apply(MemoryHistoryWrite write, Span<byte> data, Span<bool> known)
    {
        GetBytes(write).CopyTo(data[write.Offset..]);

        known.Slice(write.Offset, write.Length).Fill(true);
    }

    public ulong GetInstructions(int index) => GetInstructions(Writes[index].SpanId, Writes[index].Position);

    public TracePoint GetPoint(int index) => new(Writes[index].Position, GetInstructions(index));

    public MemoryHistoryImage GetImage(int block, int writeIndex)
    {
        var size = BlockArray[block].Size;

        var data = new byte[size];

        var known = new bool[size];

        var changed = new bool[size];

        var writes = GetBlockWrites(block);

        var dataIndex = GetDataIndex(BlockArray[block].FirstWrite);

        for (var index = 0; index < writes.Length && index <= writeIndex; index++)
        {
            var write = writes[index];

            Data.AsSpan(dataIndex, write.Length).CopyTo(data.AsSpan(write.Offset));

            known.AsSpan(write.Offset, write.Length).Fill(true);

            if (write.Type == MemoryHistoryChangeType.Changed)
            {
                changed.AsSpan(write.Offset, write.Length).Fill(true);
            }

            dataIndex += write.Length;
        }

        return new MemoryHistoryImage(data, known, changed);
    }

    public int[] GetLastWrites(int block, int writeIndex)
    {
        var lastWrites = new int[BlockArray[block].Size];

        var writes = GetBlockWrites(block);

        for (var index = 0; index < writes.Length && index <= writeIndex; index++)
        {
            lastWrites.AsSpan(writes[index].Offset, writes[index].Length).Fill(index);
        }

        return lastWrites;
    }

    public int GetWriteCount(int block, TracePosition position)
        => SortedSearch.FirstAfter<MemoryHistoryWriteEntry, TracePosition>(GetBlockWrites(block), position, w => w.Position);

    public int GetBlock(ulong address, TracePosition position)
        => GetBlock(StackAddresses, address, position) is >= 0 and var stack ? stack : GetBlock(Addresses, address, position);

    public bool IsLive(int block, TracePosition position)
        => BlockArray[block] is { InstanceCount: > 0 }
            ? GetInstance(block, position) >= 0
            : BlockArray[block].FromPosition <= position && position < BlockArray[block].ToPosition;

    public IReadOnlyList<MemoryHistoryInstance> GetInstances(int block)
        => block >= 0 && block < BlockArray.Length
            ? new ArraySegment<MemoryHistoryInstance>(Instances, BlockArray[block].FirstInstance, BlockArray[block].InstanceCount)
            : ArraySegment<MemoryHistoryInstance>.Empty;

    public int GetInstance(int block, TracePosition position)
    {
        ReadOnlySpan<MemoryHistoryInstance> instances = Instances.AsSpan(BlockArray[block].FirstInstance, BlockArray[block].InstanceCount);

        var index = SortedSearch.FirstAfter<MemoryHistoryInstance, TracePosition>(instances, position, i => i.FromPosition) - 1;

        return index >= 0 && position < instances[index].ToPosition ? index : -1;
    }

    public bool[] GetChangingBytes(int block)
    {
        var changing = new bool[BlockArray[block].Size];

        foreach (var write in GetBlockWrites(block))
        {
            if (write.Type == MemoryHistoryChangeType.Changed)
            {
                changing.AsSpan(write.Offset, write.Length).Fill(true);
            }
        }

        return changing;
    }

    public bool TryGetWrittenValue(MemoryHistoryWrite write, int offset, out uint value)
    {
        var start = offset - write.Offset;

        if (start < 0 || start + sizeof(uint) > write.Length)
        {
            value = 0;

            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(GetBytes(write)[start..]);

        return true;
    }

    public IReadOnlyList<MemoryHistoryCall> GetCalls(int block, TraceTimeline timeline, TracePosition from, TracePosition to)
    {
        if (block < 0 || block >= BlockArray.Length)
        {
            return [];
        }

        var calls = new Dictionary<int, CallBuilder>();

        var ordered = new List<CallBuilder>();

        var writes = GetBlockWrites(block);

        var image = new byte[BlockArray[block].Size];

        var firstListed = -1;

        var dataIndex = GetDataIndex(BlockArray[block].FirstWrite);

        for (var index = 0; index < writes.Length; index++)
        {
            var write = writes[index];

            var bytes = Data.AsSpan(dataIndex, write.Length);

            if (IsChange(write, from, to))
            {
                if (firstListed < 0)
                {
                    firstListed = index;
                }

                if (!calls.TryGetValue(write.SpanId, out var call))
                {
                    var first = GetWrite(BlockArray[block].FirstWrite + index);

                    call = new CallBuilder(timeline.NodeOf(first.CallNodeIndex), first, GetIterator(first));

                    calls[write.SpanId] = call;

                    ordered.Add(call);
                }

                call.Add(index, write, bytes, image);
            }

            bytes.CopyTo(image.AsSpan(write.Offset));

            dataIndex += write.Length;
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

        var calls = new HashSet<int>();

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

    public MemoryHistoryWrite GetWrite(int index)
    {
        var write = Writes[index];

        var (callNodeIndex, callIndex) = GetCall(write.SpanId);

        return new MemoryHistoryWrite(index, write.Position, callNodeIndex, callIndex, write.Offset, write.Length, write.Type);
    }

    public (int First, int Count) GetChangedWriteRange(int block, TracePosition from, TracePosition to)
    {
        var writes = GetBlockWrites(block);

        var start = SortedSearch.FirstAtOrAfter<MemoryHistoryWriteEntry, TracePosition>(writes, from, w => w.Position);

        var end = SortedSearch.FirstAfter<MemoryHistoryWriteEntry, TracePosition>(writes, to, w => w.Position, start);

        while (start < end && writes[start].Type != MemoryHistoryChangeType.Changed)
        {
            start++;
        }

        while (end > start && writes[end - 1].Type != MemoryHistoryChangeType.Changed)
        {
            end--;
        }

        return (BlockArray[block].FirstWrite + start, end - start);
    }

    public (int Offset, int Length) GetChangedExtent(MemoryHistoryWrite write)
        => (write.Offset, write.Type == MemoryHistoryChangeType.Changed ? write.Length : 0);

    private MemoryHistorySummary GetSummary(int block,
                                            TracePosition from,
                                            TracePosition to,
                                            HashSet<int> calls,
                                            Span<bool> changing)
    {
        var operators = new Dictionary<ulong, int>();

        var changeCount = 0;

        var lastCall = int.MinValue;

        var (iterator, iteratorCount) = (0ul, 0);

        foreach (var write in GetBlockWrites(block))
        {
            if (write.Position < from || write.Position > to || !MarkChanged(write, changing))
            {
                continue;
            }

            changeCount++;

            if (write.SpanId == lastCall)
            {
                iteratorCount++;

                continue;
            }

            lastCall = write.SpanId;

            calls.Add(lastCall);

            AddOperator(operators, iterator, iteratorCount);

            (iterator, iteratorCount) = (Iterators.GetValueOrDefault(GetCall(write.SpanId).CallNodeIndex), 1);
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
        => timeline.NodeOf(BlockArray[block].CallNodeIndex)?.Ancestors().FirstOrDefault(n => !AllocationFunction.IsAllocatorFrame(n.Frame));

    private MemoryHistoryCall GetAllocationCall(int block, TraceTimeline timeline, int firstListed)
    {
        var allocation = BlockArray[block];

        var writes = GetBlockWrites(block);

        var first = GetWrite(allocation.FirstWrite);

        var node = allocation.IsInline ? timeline.NodeOf(first.CallNodeIndex) : GetAllocatingCall(block, timeline);

        var regions = new List<MemoryHistoryRegion>();

        for (var index = 0; index < firstListed; index++)
        {
            regions.Add(new MemoryHistoryRegion(writes[index].Offset, writes[index].Length));
        }

        var initialised = MemoryHistoryRegion.Merge(regions);

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

    private ReadOnlySpan<MemoryHistoryWriteEntry> GetBlockWrites(int block)
        => Writes.AsSpan(BlockArray[block].FirstWrite, BlockArray[block].WriteCount);

    private (int CallNodeIndex, int CallIndex) GetCall(int spanId) => Timeline?.GetSpanCall(spanId) ?? (-1, -1);

    private ulong GetInstructions(int spanId, TracePosition position) => Timeline?.GetInstructions(spanId, position) ?? 0;

    private int GetDataIndex(int index)
    {
        var dataIndex = DataCheckpoints[index >> CheckpointShift];

        for (var previous = index & ~CheckpointMask; previous < index; previous++)
        {
            dataIndex += Writes[previous].Length;
        }

        return dataIndex;
    }

    private static int[] GetDataCheckpoints(MemoryHistoryWriteEntry[] writes)
    {
        var checkpoints = new int[(writes.Length >> CheckpointShift) + 1];

        var dataIndex = 0;

        for (var index = 0; index < writes.Length; index++)
        {
            if ((index & CheckpointMask) == 0)
            {
                checkpoints[index >> CheckpointShift] = dataIndex;
            }

            dataIndex += writes[index].Length;
        }

        return checkpoints;
    }

    private bool IsLiveAt(int block, ulong address, TracePosition position)
        => address < BlockArray[block].Address + (ulong)BlockArray[block].Size && IsLive(block, position);

    private static bool MarkChanged(MemoryHistoryWriteEntry write, Span<bool> changing)
    {
        if (write.Type != MemoryHistoryChangeType.Changed)
        {
            return false;
        }

        changing.Slice(write.Offset, write.Length).Fill(true);

        return true;
    }

    private static void AddOperator(Dictionary<ulong, int> operators, ulong iterator, int count)
    {
        if (iterator != 0 && count > 0)
        {
            operators[iterator] = operators.GetValueOrDefault(iterator) + count;
        }
    }

    private static bool IsChange(MemoryHistoryWriteEntry write, TracePosition from, TracePosition to)
        => from <= write.Position && write.Position <= to && write.Type == MemoryHistoryChangeType.Changed;

    private int GetBlock(AddressOrder order, ulong address, TracePosition position)
    {
        var found = -1;

        foreach (var at in order.Addresses.GetCandidates(address))
        {
            var block = order.Blocks[at];

            if (IsLiveAt(block, address, position) && (found < 0 || BlockArray[block].Size < BlockArray[found].Size))
            {
                found = block;
            }
        }

        return found;
    }

    private AddressOrder GetAddressOrder(bool isStack)
    {
        int[] blocks = [.. Enumerable.Range(0, BlockArray.Length)
                                     .Where(b => BlockArray[b].IsStack == isStack)
                                     .OrderBy(b => BlockArray[b].Address)];

        return new AddressOrder(blocks,
                                new AddressIndex([.. blocks.Select(b => BlockArray[b].Address)],
                                                 blocks.Length == 0 ? 0 : (ulong)blocks.Max(b => BlockArray[b].Size)));
    }

    private sealed record AddressOrder(int[] Blocks, AddressIndex Addresses);

    private sealed class CallBuilder(CallStackNode? node, MemoryHistoryWrite first, ulong iterator)
    {
        private List<MemoryHistoryRegion> Changed { get; } = [];

        private Dictionary<int, (byte Before, byte After)> Values { get; } = [];

        private int ChangeCount { get; set; }

        private int LastWrite { get; set; }

        public void Add(int writeIndex, MemoryHistoryWriteEntry write, ReadOnlySpan<byte> bytes, byte[] image)
        {
            ChangeCount++;

            LastWrite = writeIndex;

            Changed.Add(new MemoryHistoryRegion(write.Offset, write.Length));

            for (var index = 0; index < write.Length; index++)
            {
                var offset = write.Offset + index;

                var before = Values.TryGetValue(offset, out var value) ? value.Before : image[offset];

                Values[offset] = (before, bytes[index]);
            }
        }

        public MemoryHistoryCall Build()
            => new(node,
                   iterator,
                   first.CallIndex,
                   first.Position,
                   LastWrite,
                   ChangeCount,
                   MemoryHistoryRegion.Merge(Changed),
                   MemoryHistoryRegion.Merge(Values.Where(v => v.Value.Before != v.Value.After).Select(v => new MemoryHistoryRegion(v.Key, 1))));
    }

    private sealed class WriteList(MemoryHistoryStore store, int first, int count) : IReadOnlyList<MemoryHistoryWrite>
    {
        public int Count => count;

        public MemoryHistoryWrite this[int index]
            => (uint)index < (uint)count ? store.GetWrite(first + index) : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<MemoryHistoryWrite> GetEnumerator()
        {
            for (var index = 0; index < count; index++)
            {
                yield return store.GetWrite(first + index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ReadList(MemoryHistoryStore store, int first, int count) : IReadOnlyList<MemoryHistoryRead>
    {
        public int Count => count;

        public MemoryHistoryRead this[int index]
            => (uint)index < (uint)count ? store.GetRead(first + index) : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<MemoryHistoryRead> GetEnumerator()
        {
            for (var index = 0; index < count; index++)
            {
                yield return store.GetRead(first + index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
