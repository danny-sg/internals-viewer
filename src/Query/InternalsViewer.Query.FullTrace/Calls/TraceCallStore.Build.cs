using System.IO.Compression;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    internal static TraceCallStore Build(ReplayCallLog log, TraceNodeMap nodes)
    {
        var functions = log.GetChunks();

        using var compressed = new MemoryStream();

        foreach (var (key, chunks) in functions)
        {
            var ends = log.CallEnds.GetValueOrDefault(key);

            var stackArguments = log.StackArguments.GetValueOrDefault(key);

            if (ends is not null || stackArguments is not null)
            {
                Apply(chunks, ends, stackArguments, compressed);
            }
        }

        return new TraceCallStore(functions, TraceLocatorSet.Build(log), nodes);
    }

    internal static Chunk Compress(Span<ulong> columns, int calls, int stackSlots, MemoryStream compressed)
    {
        TraceCallColumnSet.Encode(columns, calls);

        compressed.SetLength(0);

        using (var stream = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            stream.Write(MemoryMarshal.AsBytes(columns));
        }

        return new Chunk(calls, stackSlots, compressed.ToArray());
    }

    private static void Apply(Chunk[] chunks,
                              List<ReplayCallLog.CallEnd>? ends,
                              ReplayCallLog.StackArgumentList? stackArguments,
                              MemoryStream compressed)
    {
        var first = 0;

        var next = 0;

        var slots = stackArguments?.SlotCount ?? 0;

        ulong[] buffer = [];

        for (var index = 0; index < chunks.Length; index++)
        {
            var calls = chunks[index].CallCount;

            var inChunk = ends?.Where(e => e.CallIndex >= first && e.CallIndex < first + calls).ToArray() ?? [];

            if (inChunk.Length == 0 && slots == 0)
            {
                first += calls;

                continue;
            }

            var length = calls * (TraceCallColumnSet.ValueCount + slots);

            if (buffer.Length < length)
            {
                buffer = new ulong[length];
            }

            var columns = Decompress(chunks[index], ref buffer);

            buffer.AsSpan(calls * TraceCallColumnSet.ValueCount, calls * slots).Clear();

            if (stackArguments is not null)
            {
                while (next < stackArguments.Count && stackArguments.CallIndexes[next] < first + calls)
                {
                    var callIndex = (int)stackArguments.CallIndexes[next] - first;

                    for (var slot = 0; callIndex >= 0 && slot < slots; slot++)
                    {
                        buffer[(TraceCallColumnSet.ValueCount + slot) * calls + callIndex] = stackArguments.Values[next * slots + slot];
                    }

                    next++;
                }
            }

            foreach (var (callIndex, toPosition, returnValue, returned) in inChunk)
            {
                columns.SetToPosition((int)callIndex - first, toPosition, returnValue, returned);
            }

            chunks[index] = Compress(buffer.AsSpan(0, length), calls, slots, compressed);

            first += calls;
        }
    }
}
