using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Buffers.Binary;
using System.Collections;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed partial class MemoryHistoryStore
{
    private const int ArrayCountSize = sizeof(ulong);

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

    private MemoryHistoryBlock[] BlockArray { get; }

    private MemoryHistoryAccessTable Writes { get; }

    private IReadOnlyDictionary<int, ulong> Iterators { get; }

    private TraceTimeline? Timeline { get; }

    private MemoryHistoryAccessTable Reads { get; }

    private int[] ReadStarts { get; }

    private MemoryHistoryInstance[] Instances { get; }

    private BlockCache<uint> ArrayCounts => field ??= new(FindArrayCount);

    private AddressOrder Addresses => field ??= GetAddressOrder(isStack: false);

    private AddressOrder StackAddresses => field ??= GetAddressOrder(isStack: true);

    public (ulong FirstElement, uint Count)? GetArray(int block)
        => BlockArray[block].IsArray && ArrayCounts.Get(block) is > 0 and var count
            ? (BlockArray[block].Address + ArrayCountSize, count)
            : null;

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

    public TracePosition GetCreatedPosition(int block)
        => BlockArray[block] is { IsInline: true, WriteCount: > 0 } inline ? Writes[inline.FirstWrite].Position : BlockArray[block].FromPosition;

    public bool IsCreatedWithin(int block, TracePosition from, TracePosition to)
        => GetCreatedPosition(block) is var created && from <= created && created <= to;

    public CallTreeNode? GetAllocatingCall(int block, TraceTimeline timeline)
        => timeline.GetNode(BlockArray[block].CallNodeIndex)?.GetAncestors().FirstOrDefault(n => !AllocationFunction.IsAllocatorFrame(n.Frame));

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

    private uint FindArrayCount(int block)
    {
        var elementBytes = BlockArray[block].Size - ArrayCountSize;

        foreach (var write in GetWrites(block))
        {
            if (TryGetWrittenValue(write, 0, out var count)
                && TryGetWrittenValue(write, sizeof(uint), out var high)
                && high == 0
                && count > 0
                && elementBytes % count == 0)
            {
                return count;
            }
        }

        return 0;
    }

    private sealed record AddressOrder(int[] Blocks, AddressIndex Addresses);

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

        public void Dispose() => Table.ReturnWindow(Window);

        private int Load(int index)
        {
            if (!Window.Contains(First + index))
            {
                Table.Read(First + index, First, First + Length, Window);
            }

            return First + index - Window.First;
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
