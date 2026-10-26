using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Watches;
using InternalsViewer.Query.FullTrace.Native;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

internal static class MemoryHistoryStores
{
    private const ulong Writer = 0x7FF8_0080_0010;

    private const uint Thread = 9;

    public static MemoryBlock Block(ulong pointer, ulong bytes, ulong from = 1)
        => new(pointer, bytes, Thread, At(from), TracePosition.Unknown);

    public static MemoryHistoryStore Build(MemoryBlock[] blocks, (ulong Sequence, ulong Address, byte[] Bytes)[] writes)
    {
        var data = new List<byte>();

        var hits = new List<ChunkHit>();

        foreach (var (sequence, address, bytes) in writes)
        {
            hits.Add(ChunkHit.From(new WatchHit(sequence, 0, address, Writer, Thread, (uint)bytes.Length, 0, 1), data.Count));

            data.AddRange(bytes);
        }

        return MemoryHistoryBuilder.Build([.. blocks.Select(b => new MemoryHistoryAllocation(b, false))],
                                          new ulong[blocks.Length],
                                          new Dictionary<ulong, TracePosition>(),
                                          new WatchResult([new WatchChunk([.. hits], [.. data])], 1),
                                          null).Store;
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
