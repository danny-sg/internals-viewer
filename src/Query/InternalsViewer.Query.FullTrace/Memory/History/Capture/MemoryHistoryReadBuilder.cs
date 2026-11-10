using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class MemoryHistoryReadBuilder(WatchedBlockIndex lookup, TraceTimeline? timeline)
{
    private const int FirstSweep = 64;

    public MemoryHistoryCaptureCounter Counts { get; } = new();

    private Dictionary<uint, ThreadMarkTable> Threads { get; } = [];

    private ChunkedList<ClosedMark> Closed { get; set; } = [];

    private TraceInnermostCallIndex Calls { get; } = new(timeline);

    private bool IsOrdered { get; set; } = true;

    public void Add(WatchHit hit)
    {
        Counts.ReceivedCount++;

        var position = hit.Position;

        if (lookup.GetInnermost(hit.Address, position) is not { } found)
        {
            Counts.OutsideCount++;

            return;
        }

        var block = lookup.Blocks[found];

        if (block.IsBufferPool)
        {
            Counts.BufferPoolCount++;

            return;
        }

        var spanId = Calls.GetSpanId(hit.Thread, position, out var spanEnd);

        var offset = (int)(hit.Address - block.Address);

        var end = (int)Math.Min(block.Size, hit.Address - block.Address + hit.Size);

        var table = GetTable(hit.Thread);

        if (position < table.Latest)
        {
            IsOrdered = false;
        }
        else
        {
            table.Latest = position;
        }

        ref var mark = ref CollectionsMarshal.GetValueRefOrAddDefault(table.Open, (found, spanId), out var exists);

        mark = exists
            ? mark with { Mark = mark.Mark.Add(position, offset, end) }
            : new OpenMark(new ReadMark(position, offset, end, 1), spanEnd);

        Counts.CapturedCount++;

        if (table.Open.Count >= table.SweepAt)
        {
            Sweep(table);
        }
    }

    public void Close()
    {
        foreach (var table in Threads.Values)
        {
            foreach (var ((block, spanId), open) in table.Open)
            {
                Closed.Add(new ClosedMark(block, spanId, open.Mark));
            }
        }

        Threads.Clear();

        if (!IsOrdered)
        {
            MergeRepeats();
        }
    }

    public bool[] GetReadBlocks(int count)
    {
        var blocks = new bool[count];

        for (var index = 0; index < Closed.Count; index++)
        {
            if (Closed[index].Block >= 0)
            {
                blocks[Closed[index].Block] = true;
            }
        }

        return blocks;
    }

    public (MemoryHistoryAccessEntry[] Reads, int[] Starts) Build(bool[] isKept, int keptCount)
    {
        var kept = new int[isKept.Length];

        var next = 0;

        for (var index = 0; index < isKept.Length; index++)
        {
            kept[index] = isKept[index] ? next++ : -1;
        }

        var starts = new int[keptCount + 1];

        for (var index = 0; index < Closed.Count; index++)
        {
            if (Closed[index].Block >= 0 && kept[Closed[index].Block] >= 0)
            {
                starts[kept[Closed[index].Block] + 1]++;
            }
        }

        for (var index = 1; index < starts.Length; index++)
        {
            starts[index] += starts[index - 1];
        }

        var reads = new MemoryHistoryAccessEntry[starts[^1]];

        var placed = new int[keptCount];

        for (var index = 0; index < Closed.Count; index++)
        {
            var (block, spanId, mark) = Closed[index];

            if (block < 0)
            {
                continue;
            }

            if (kept[block] < 0)
            {
                Counts.UnwrittenCount += mark.Count;

                continue;
            }

            var target = starts[kept[block]] + placed[kept[block]]++;

            reads[target] = new MemoryHistoryAccessEntry(mark.First,
                                                         spanId,
                                                         mark.Start,
                                                         mark.End - mark.Start,
                                                         mark.Count,
                                                         MemoryHistoryAccessType.Read);
        }

        Closed = [];

        for (var block = 0; block < keptCount; block++)
        {
            reads.AsSpan(starts[block], starts[block + 1] - starts[block])
                 .Sort(static (left, right) => left.Position.CompareTo(right.Position));
        }

        return (reads, starts);
    }

    private ThreadMarkTable GetTable(uint thread)
    {
        if (!Threads.TryGetValue(thread, out var table))
        {
            table = new ThreadMarkTable { SweepAt = FirstSweep };

            Threads[thread] = table;
        }

        return table;
    }

    private void Sweep(ThreadMarkTable table)
    {
        foreach (var ((block, spanId), open) in table.Open)
        {
            if (open.End.IsKnown && open.End <= table.Latest)
            {
                Closed.Add(new ClosedMark(block, spanId, open.Mark));

                table.Open.Remove((block, spanId));
            }
        }

        table.SweepAt = Math.Max(FirstSweep, table.Open.Count * 2);
    }

    private void MergeRepeats()
    {
        var firsts = new Dictionary<(int Block, int SpanId), int>(Closed.Count);

        for (var index = 0; index < Closed.Count; index++)
        {
            ref var repeat = ref Closed[index];

            if (!firsts.TryAdd((repeat.Block, repeat.SpanId), index))
            {
                ref var first = ref Closed[firsts[(repeat.Block, repeat.SpanId)]];

                first = first with { Mark = first.Mark.Merge(repeat.Mark) };

                repeat = repeat with { Block = -1 };
            }
        }
    }

    private readonly record struct ReadMark(TracePosition First, int Start, int End, int Count)
    {
        public ReadMark Add(TracePosition position, int start, int end)
            => new(position < First ? position : First, Math.Min(start, Start), Math.Max(end, End), Count + 1);

        public ReadMark Merge(ReadMark other)
            => new(other.First < First ? other.First : First, Math.Min(other.Start, Start), Math.Max(other.End, End), Count + other.Count);
    }

    private readonly record struct OpenMark(ReadMark Mark, TracePosition End);

    private readonly record struct ClosedMark(int Block, int SpanId, ReadMark Mark);

    private sealed class ThreadMarkTable
    {
        public Dictionary<(int Block, int SpanId), OpenMark> Open { get; } = [];

        public TracePosition Latest { get; set; }

        public int SweepAt { get; set; }
    }
}
