using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Batches;

internal static class BatchDeepDataLocator
{
    private const uint PageMagic = 0x53445042;

    private const ulong FirstPageOffset = 0x18;

    private const ulong NextPageOffset = 0x08;

    private const ulong PageMagicOffset = 0x10;

    private const ulong PageCapacityOffset = 0x14;

    private const ulong PageDataOffset = 0x18;

    private const int MaximumPages = 4096;

    public static List<(ulong Start, ulong End, int VectorId)> GetPages(ITraceReplay replay, ulong batch)
    {
        var pages = new List<(ulong Start, ulong End, int VectorId)>();

        if (!replay.TryRead(batch + BatchHook.BatchVectorsOffset, out ulong vectors)
            || !replay.TryRead(batch + BatchHook.BatchVectorCountOffset, out int count)
            || vectors < BatchHook.SmallestAddress
            || count is <= 0 or > BatchHook.MaximumBatchVectors)
        {
            return pages;
        }

        for (var index = 0; index < count; index++)
        {
            if (!replay.TryRead(vectors + (ulong)index * BatchHook.BatchVectorEntrySize + FirstPageOffset, out ulong page))
            {
                continue;
            }

            for (var hop = 0; hop < MaximumPages && page >= BatchHook.SmallestAddress; hop++)
            {
                if (!replay.TryRead(page + PageMagicOffset, out uint magic)
                    || magic != PageMagic
                    || !replay.TryRead(page + PageCapacityOffset, out ushort capacity))
                {
                    break;
                }

                pages.Add((page + PageDataOffset, page + PageDataOffset + capacity, index + 1));

                if (!replay.TryRead(page + NextPageOffset, out page))
                {
                    break;
                }
            }
        }

        return pages;
    }
}
