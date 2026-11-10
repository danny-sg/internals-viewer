using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Allocation.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class MemoryHistoryWriteBuilder(WatchedBlockIndex lookup, WatchHitCallResolver stacks)
{
    private const ulong PageSize = 1ul << WatchedBlockIndex.PageShift;

    public MemoryHistoryCaptureCounter Counts { get; } = new();

    public int[] Written { get; } = new int[lookup.Blocks.Count];

    public MemoryHistoryAccessEntry[] Writes { get; private set; } = [];

    public byte[] Data { get; private set; } = [];

    public Dictionary<int, ulong> Iterators { get; } = [];

    private int WriteCount { get; set; }

    private int DataLength { get; set; }

    private int[] WrittenBytes { get; } = new int[lookup.Blocks.Count];

    private int[] Next { get; set; } = [];

    private int[] NextData { get; set; } = [];

    private bool IsWriting { get; set; }

    private int? Writer { get; set; }

    private (ChunkHit Hit, int Block, int End, int Length)? LastInitialised { get; set; }

    private int? LastWriter { get; set; }

    public void Count(ChunkHit hit, ReadOnlySpan<byte> bytes)
    {
        Writer = null;

        var first = DataLength;

        var (isInBlock, isBufferPool) = Apply(hit, bytes);

        Counts.Add(DataLength > first, isBufferPool, isInBlock);
    }

    public void BeginWriting(int[] firstWrites)
    {
        foreach (var block in lookup.Blocks)
        {
            if (block.Known is { } known)
            {
                Array.Clear(known);
            }
        }

        Writes = new MemoryHistoryAccessEntry[WriteCount];

        Data = new byte[DataLength];

        Next = firstWrites;

        NextData = new int[WrittenBytes.Length];

        var dataStart = 0;

        for (var index = 0; index < WrittenBytes.Length; index++)
        {
            NextData[index] = dataStart;

            dataStart += WrittenBytes[index];
        }

        LastInitialised = null;

        LastWriter = null;

        IsWriting = true;
    }

    public void Write(ChunkHit hit, ReadOnlySpan<byte> bytes)
    {
        Writer = null;

        Apply(hit, bytes);
    }

    public void ApplySnapshot(int block, ChunkHit hit, ReadOnlySpan<byte> bytes)
    {
        Writer = null;

        AddChanges(hit, block, (int)(hit.Address - lookup.Blocks[block].Address), bytes);
    }

    private (bool IsInBlock, bool IsBufferPool) Apply(ChunkHit hit, ReadOnlySpan<byte> bytes)
    {
        var address = hit.Address;

        var isInBlock = false;

        var isBufferPool = false;

        while (bytes.Length > 0)
        {
            var pageEnd = (address & ~(PageSize - 1)) + PageSize;

            if (lookup.GetInnermost(address, hit.Position) is not { } found)
            {
                var skip = lookup.HasPage(address) ? 1 : (int)Math.Min((ulong)bytes.Length, pageEnd - address);

                address += (ulong)skip;

                bytes = bytes[skip..];

                continue;
            }

            var block = lookup.Blocks[found];

            var end = Math.Min(Math.Min(block.Address + block.Size, pageEnd), lookup.GetNextInner(found, address, hit.Position));

            var length = (int)Math.Min((ulong)bytes.Length, end - address);

            isInBlock = true;

            isBufferPool |= block.IsBufferPool;

            if (!block.IsBufferPool)
            {
                AddChanges(hit, found, (int)(address - block.Address), bytes[..length]);
            }

            address += (ulong)length;

            bytes = bytes[length..];
        }

        return (isInBlock, isBufferPool);
    }

    private void AddChanges(ChunkHit hit, int found, int offset, ReadOnlySpan<byte> bytes)
    {
        var block = lookup.Blocks[found];

        block.Image ??= new byte[block.Size];

        block.Known ??= new bool[block.Size];

        var position = 0;

        while (position < bytes.Length)
        {
            if (GetChangeType(block, offset + position, bytes[position]) is not { } type)
            {
                position++;

                continue;
            }

            var start = position;

            while (position < bytes.Length && GetChangeType(block, offset + position, bytes[position]) == type)
            {
                position++;
            }

            var run = bytes[start..position];

            run.CopyTo(block.Image.AsSpan(offset + start));

            block.Known.AsSpan(offset + start, run.Length).Fill(true);

            var isContinuation = IsContinuation(hit, found, offset + start, run.Length, type);

            if (IsWriting && isContinuation)
            {
                Extend(found, run);
            }
            else if (IsWriting)
            {
                Add(hit, found, offset + start, run, type);
            }
            else
            {
                if (!isContinuation)
                {
                    Written[found]++;

                    WriteCount++;
                }

                DataLength += run.Length;

                WrittenBytes[found] += run.Length;
            }

            Remember(hit, found, offset + start, run.Length, type, isContinuation);
        }
    }

    private bool IsContinuation(ChunkHit hit, int block, int offset, int length, MemoryHistoryAccessType type)
    {
        if (type != MemoryHistoryAccessType.Initialised
            || LastInitialised is not { } last
            || last.Block != block
            || last.End != offset
            || last.Hit.Thread != hit.Thread
            || last.Length + length > ushort.MaxValue)
        {
            return false;
        }

        var writer = Writer ??= GetWriter(hit);

        var previous = LastWriter ??= GetWriter(last.Hit);

        return writer >= 0 && writer == previous;
    }

    private void Remember(ChunkHit hit, int block, int offset, int length, MemoryHistoryAccessType type, bool isContinuation)
    {
        if (type != MemoryHistoryAccessType.Initialised)
        {
            LastInitialised = null;

            return;
        }

        if (isContinuation && LastInitialised is { } last)
        {
            LastInitialised = (last.Hit, block, offset + length, last.Length + length);

            return;
        }

        LastInitialised = (hit, block, offset + length, length);

        LastWriter = Writer;
    }

    private void Extend(int block, ReadOnlySpan<byte> run)
    {
        var target = Next[block] - 1;

        Writes[target] = Writes[target] with { Length = Writes[target].Length + run.Length };

        AddData(block, run);
    }

    private void Add(ChunkHit hit, int block, int offset, ReadOnlySpan<byte> run, MemoryHistoryAccessType type)
    {
        var spanId = Writer ??= GetWriter(hit);

        Writes[Next[block]++] = new MemoryHistoryAccessEntry(hit.Position, spanId, offset, run.Length, 1, type);

        AddData(block, run);
    }

    private void AddData(int block, ReadOnlySpan<byte> run)
    {
        run.CopyTo(Data.AsSpan(NextData[block]));

        NextData[block] += run.Length;
    }

    private int GetWriter(ChunkHit hit)
    {
        var spanId = stacks.GetWriter(hit.Thread, hit.Position);

        if (stacks.GetCallNodeIndex(spanId) is >= 0 and var callNodeIndex && stacks.GetIterator(callNodeIndex) is not 0 and var iterator)
        {
            Iterators[callNodeIndex] = iterator;
        }

        return spanId;
    }

    private static MemoryHistoryAccessType? GetChangeType(WatchedBlock block, int offset, byte value)
    {
        if (!block.Known![offset])
        {
            return MemoryHistoryAccessType.Initialised;
        }

        return block.Image![offset] != value ? MemoryHistoryAccessType.Changed : null;
    }
}
