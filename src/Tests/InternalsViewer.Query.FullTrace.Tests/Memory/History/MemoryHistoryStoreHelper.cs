using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Capture;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Tests.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Timeline;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

internal static class MemoryHistoryStoreHelper
{
    private const ulong Writer = 0x7FF8_0080_0010;

    private const uint Thread = 9;

    public static MemoryBlock Block(ulong pointer, ulong bytes, ulong from = 1, ulong? to = null)
        => new(pointer, bytes, Thread, At(from), to is { } end ? At(end) : TracePosition.Unknown);

    public static TraceTimeline Calls(params (ulong From, ulong To)[] calls)
    {
        var builder = new TraceTimeline.Builder();

        builder.Add([.. calls.Select((c, i) => new CallSpan(c.From, 0, c.To, 0, c.From, c.To, 0, Thread, (uint)i, 1))]);

        return builder.Build([new CallNode(-1, 0x10, 0, (ulong)calls.Length)]);
    }

    public static MemoryHistoryStore Build(MemoryBlock[] blocks,
                                           (ulong Sequence, ulong Address, byte[] Bytes)[] writes,
                                           TraceTimeline? timeline = null,
                                           bool isArray = false)
    {
        var data = new List<byte>();

        var hits = new List<ChunkHit>();

        foreach (var (sequence, address, bytes) in writes)
        {
            hits.Add(ChunkHit.Create(new WatchHit(sequence, 0, address, Writer, Thread, (uint)bytes.Length, 0, 1), data.Count));

            data.AddRange(bytes);
        }

        return new MemoryHistoryBuilder([.. blocks.Select(b => new MemoryHistoryAllocation(b, false, IsArray: isArray))],
                                                  new ulong[blocks.Length],
                                                  new Dictionary<ulong, TracePosition>(),
                                                  timeline).Build(new WatchResult([new([.. hits], [.. data])], 1)).Store;
    }

    public static MemoryHistoryStore BuildWithReads(MemoryBlock[] blocks,
                                                    (ulong Sequence, ulong Address, byte[] Bytes)[] writes,
                                                    (ulong Sequence, ulong Address, uint Size)[] reads,
                                                    TraceTimeline timeline)
    {
        var data = new List<byte>();

        var hits = new List<ChunkHit>();

        foreach (var (sequence, address, bytes) in writes)
        {
            hits.Add(ChunkHit.Create(new WatchHit(sequence, 0, address, Writer, Thread, (uint)bytes.Length, 0, 1), data.Count));

            data.AddRange(bytes);
        }

        var recording = new MemoryHistoryBuilder([.. blocks.Select(b => new MemoryHistoryAllocation(b, false))],
                                                           new ulong[blocks.Length],
                                                           new Dictionary<ulong, TracePosition>(),
                                                           timeline);

        foreach (var (sequence, address, size) in reads)
        {
            recording.AddRead(new WatchHit(sequence, 0, address, Writer, Thread, size, 0, 0));
        }

        return recording.Build(new WatchResult([new([.. hits], [.. data])], 1)).Store;
    }

    public static int GetBlock(MemoryHistoryStore store, ulong address)
    {
        for (var index = 0; index < store.Blocks.Count; index++)
        {
            if (store.Blocks[index].Address == address)
            {
                return index;
            }
        }

        return -1;
    }
}
