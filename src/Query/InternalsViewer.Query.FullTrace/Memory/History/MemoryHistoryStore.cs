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
    internal const int WriteCacheSlots = 128;

    internal const int ReadCacheSlots = 32;

    internal MemoryHistoryStore(MemoryHistoryBlock[] blocks,
                                MemoryHistoryAccessTable writes,
                                IReadOnlyDictionary<int, ulong> iterators,
                                TraceTimeline? timeline,
                                MemoryHistoryAccessTable? reads = null,
                                int[]? readStarts = null,
                                MemoryHistoryInstance[]? instances = null)
    {
        Blocks = blocks;
        BlockArray = blocks;
        Writes = writes;
        Iterators = iterators;
        Timeline = timeline;
        Reads = reads ?? MemoryHistoryAccessTable.Empty;
        ReadStarts = readStarts ?? [];
        Instances = instances ?? [];
    }

    public static MemoryHistoryStore Empty { get; } = new([], MemoryHistoryAccessTable.Empty, new Dictionary<int, ulong>(), null);

    public IReadOnlyList<MemoryHistoryBlock> Blocks { get; }

    public int WriteCount => Writes.Count;

    public int ReadCount => Reads.Count;

    public bool HasReads => ReadStarts.Length > 0;

    internal long Bytes => WriteBytes + ReadBytes + InstanceBytes;

    internal long WriteBytes => Writes.Bytes;

    internal long ReadBytes => Reads.Bytes + ReadStarts.Length * (long)sizeof(int);

    internal long InstanceBytes => Instances.Length * (long)Unsafe.SizeOf<MemoryHistoryInstance>();

    private MemoryHistoryBlock[] BlockArray { get; }

    private MemoryHistoryAccessTable Writes { get; }

    private IReadOnlyDictionary<int, ulong> Iterators { get; }

    private TraceTimeline? Timeline { get; }

    private MemoryHistoryAccessTable Reads { get; }

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

        var start = Reads.GetLowerBound(first, first + count, from, strict: false);

        var end = Reads.GetLowerBound(start, first + count, to, strict: true);

        return (start, end - start);
    }

    public MemoryHistoryRead GetRead(int index)
    {
        var read = Reads[index];

        var (callNodeIndex, callIndex) = GetCall(read.SpanId);

        return new MemoryHistoryRead(read.Position, callNodeIndex, callIndex, read.Offset, read.Length, read.Count);
    }

    public TracePoint GetReadPoint(int index)
    {
        var read = Reads[index];

        return new TracePoint(read.Position, GetInstructions(read.SpanId, read.Position));
    }

    public byte[] GetBytes(MemoryHistoryWrite write)
    {
        var bytes = new byte[write.Length];

        CopyBytes(write, 0, bytes);

        return bytes;
    }

    public void CopyBytes(MemoryHistoryWrite write, int start, Span<byte> destination)
        => Writes.CopyBytes(write.Index, start, destination);

    public void Apply(MemoryHistoryWrite write, Span<byte> data, Span<bool> known)
    {
        CopyBytes(write, 0, data.Slice(write.Offset, write.Length));

        known.Slice(write.Offset, write.Length).Fill(true);
    }

    public ulong GetInstructions(int index)
    {
        var write = Writes[index];

        return GetInstructions(write.SpanId, write.Position);
    }

    public TracePoint GetPoint(int index)
    {
        var write = Writes[index];

        return new TracePoint(write.Position, GetInstructions(write.SpanId, write.Position));
    }

    public MemoryHistoryImage GetImage(int block, int writeIndex)
    {
        var size = BlockArray[block].Size;

        var data = new byte[size];

        var known = new bool[size];

        var changed = new bool[size];

        using var writes = GetBlockWrites(block);

        for (var index = 0; index < writes.Length && index <= writeIndex; index++)
        {
            var write = writes[index];

            writes.GetBytes(index).CopyTo(data.AsSpan(write.Offset));

            known.AsSpan(write.Offset, write.Length).Fill(true);

            if (write.Type == MemoryHistoryAccessType.Changed)
            {
                changed.AsSpan(write.Offset, write.Length).Fill(true);
            }
        }

        return new MemoryHistoryImage(data, known, changed);
    }

    public int[] GetLastWrites(int block, int writeIndex)
    {
        var lastWrites = new int[BlockArray[block].Size];

        using var writes = GetBlockWrites(block);

        for (var index = 0; index < writes.Length && index <= writeIndex; index++)
        {
            lastWrites.AsSpan(writes[index].Offset, writes[index].Length).Fill(index);
        }

        return lastWrites;
    }

    public int GetWriteCount(int block, TracePosition position)
    {
        var first = BlockArray[block].FirstWrite;

        return Writes.GetLowerBound(first, first + BlockArray[block].WriteCount, position, strict: true) - first;
    }

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

        using var writes = GetBlockWrites(block);

        foreach (var write in writes)
        {
            if (write.Type == MemoryHistoryAccessType.Changed)
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

        using var writes = GetBlockWrites(block);

        var image = new byte[BlockArray[block].Size];

        var firstListed = -1;

        for (var index = 0; index < writes.Length; index++)
        {
            var write = writes[index];

            var bytes = writes.GetBytes(index);

            if (IsChange(write, from, to))
            {
                if (firstListed < 0)
                {
                    firstListed = index;
                }

                if (!calls.TryGetValue(write.SpanId, out var call))
                {
                    var first = GetWrite(BlockArray[block].FirstWrite + index, write);

                    call = new CallBuilder(timeline.NodeOf(first.CallNodeIndex), first, GetIterator(first));

                    calls[write.SpanId] = call;

                    ordered.Add(call);
                }

                call.Add(index, write, bytes, image);
            }

            bytes.CopyTo(image.AsSpan(write.Offset));
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

    public MemoryHistoryWrite GetWrite(int index) => GetWrite(index, Writes[index]);

    public (int First, int Count) GetChangedWriteRange(int block, TracePosition from, TracePosition to)
    {
        using var writes = GetBlockWrites(block);

        var start = writes.GetFirstAtOrAfter(from);

        var end = writes.GetFirstAfter(to, start);

        while (start < end && writes[start].Type != MemoryHistoryAccessType.Changed)
        {
            start++;
        }

        while (end > start && writes[end - 1].Type != MemoryHistoryAccessType.Changed)
        {
            end--;
        }

        return (BlockArray[block].FirstWrite + start, end - start);
    }

    public (int Offset, int Length) GetChangedExtent(MemoryHistoryWrite write)
        => (write.Offset, write.Type == MemoryHistoryAccessType.Changed ? write.Length : 0);

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

        using var writes = GetBlockWrites(block);

        foreach (var write in writes)
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

        using var writes = GetBlockWrites(block);

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

    private WriteRange GetBlockWrites(int block) => new(Writes, BlockArray[block].FirstWrite, BlockArray[block].WriteCount);

    private MemoryHistoryWrite GetWrite(int index, MemoryHistoryAccessEntry write)
    {
        var (callNodeIndex, callIndex) = GetCall(write.SpanId);

        return new MemoryHistoryWrite(index, write.Position, callNodeIndex, callIndex, write.Offset, (ushort)write.Length, write.Type);
    }

    private (int CallNodeIndex, int CallIndex) GetCall(int spanId) => Timeline?.GetSpanCall(spanId) ?? (-1, -1);

    private ulong GetInstructions(int spanId, TracePosition position) => Timeline?.GetInstructions(spanId, position) ?? 0;

    private bool IsLiveAt(int block, ulong address, TracePosition position)
        => address < BlockArray[block].Address + (ulong)BlockArray[block].Size && IsLive(block, position);

    private static bool MarkChanged(MemoryHistoryAccessEntry write, Span<bool> changing)
    {
        if (write.Type != MemoryHistoryAccessType.Changed)
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

    private static bool IsChange(MemoryHistoryAccessEntry write, TracePosition from, TracePosition to)
        => from <= write.Position && write.Position <= to && write.Type == MemoryHistoryAccessType.Changed;

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

        public void Add(int writeIndex, MemoryHistoryAccessEntry write, ReadOnlySpan<byte> bytes, byte[] image)
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

    private readonly struct WriteRange : IDisposable
    {
        public WriteRange(MemoryHistoryAccessTable table, int first, int count)
        {
            Table = table;
            First = first;
            Length = count;
            Window = table.RentWindow();
        }

        public int Length { get; }

        private MemoryHistoryAccessTable Table { get; }

        private int First { get; }

        private MemoryHistoryAccessTable.ChunkBuffer Window { get; }

        public MemoryHistoryAccessEntry this[int index] => Window.Entries[Load(index)];

        public ReadOnlySpan<byte> GetBytes(int index) => Window.GetBytes(Load(index));

        public int GetFirstAtOrAfter(TracePosition position, int from = 0)
            => Table.GetLowerBound(First + from, First + Length, position, strict: false) - First;

        public int GetFirstAfter(TracePosition position, int from = 0)
            => Table.GetLowerBound(First + from, First + Length, position, strict: true) - First;

        public Enumerator GetEnumerator() => new(this);

        public void Dispose() => Table.ReturnWindow(Window);

        private int Load(int index)
        {
            if (!Window.Contains(First + index))
            {
                Table.Read(First + index, First, First + Length, Window);
            }

            return First + index - Window.First;
        }

        public struct Enumerator(WriteRange range)
        {
            public readonly MemoryHistoryAccessEntry Current => range[Index];

            private int Index { get; set; } = -1;

            public bool MoveNext() => ++Index < range.Length;
        }
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
