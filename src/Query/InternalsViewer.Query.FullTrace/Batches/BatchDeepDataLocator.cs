using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Batches;

internal static class BatchDeepDataLocator
{
    private const uint LayoutPageMagic = 0x53445042;

    private const ulong LayoutFirstPageOffset = 0x18;

    private const ulong LayoutNextPageOffset = 0x08;

    private const ulong LayoutPageMagicOffset = 0x10;

    private const ulong LayoutPageCapacityOffset = 0x14;

    private const ulong LayoutPageDataOffset = 0x18;

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
            if (!replay.TryRead(vectors + (ulong)index * BatchHook.BatchVectorEntrySize + LayoutFirstPageOffset, out ulong page))
            {
                continue;
            }

            for (var hop = 0; hop < MaximumPages && page >= BatchHook.SmallestAddress; hop++)
            {
                if (!replay.TryRead(page + LayoutPageMagicOffset, out uint magic)
                    || magic != LayoutPageMagic
                    || !replay.TryRead(page + LayoutPageCapacityOffset, out ushort capacity))
                {
                    break;
                }

                pages.Add((page + LayoutPageDataOffset, page + LayoutPageDataOffset + capacity, index + 1));

                if (!replay.TryRead(page + LayoutNextPageOffset, out page))
                {
                    break;
                }
            }
        }

        return pages;
    }
}
