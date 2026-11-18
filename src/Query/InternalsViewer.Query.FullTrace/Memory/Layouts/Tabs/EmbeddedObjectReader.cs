using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;

public static class EmbeddedObjectReader
{
    public static List<EmbeddedObject> Read(AddressMap map,
                                            MemoryHistoryStore store,
                                            TraceCallStore? calls,
                                            int block,
                                            TracePosition position)
    {
        var item = store.Blocks[block];

        var classNames = new Dictionary<int, string?>();

        return Read(map.GetEmbedded(item.Address, item.Size, item.FromPosition, position),
                    store,
                    block,
                    n => GetClassName(calls, classNames, n));
    }

    internal static List<EmbeddedObject> Read(List<AddressMapEntry> entries,
                                              MemoryHistoryStore store,
                                              int block,
                                              Func<int, string?> getClassName)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var address = store.Blocks[block].Address;

        var size = store.Blocks[block].Size;

        int[] starts = [.. entries.Select(e => (int)(e.Address - address))];

        var ends = new int[entries.Count];

        foreach (var write in store.GetWrites(block))
        {
            Measure(write.CallNodeIndex, write.Offset, write.Length);
        }

        foreach (var read in store.GetReads(block))
        {
            Measure(read.CallNodeIndex, read.Offset, read.Length);
        }

        return
        [
            .. entries.Select((e, i) => new EmbeddedObject(e.Address, (e.VtableClassName ?? e.ClassName)!, Math.Max(0, ends[i] - starts[i])))
        ];

        void Measure(int callNodeIndex, int offset, int length)
        {
            var index = SortedSearch.FirstAfter<int>(starts, offset) - 1;

            if (index < 0)
            {
                return;
            }

            var limit = index + 1 < starts.Length ? starts[index + 1] : size;

            if (offset < limit
                && getClassName(callNodeIndex) is { } className
                && (className == entries[index].ClassName || className == entries[index].VtableClassName))
            {
                ends[index] = Math.Max(ends[index], Math.Min(offset + length, limit));
            }
        }
    }

    private static string? GetClassName(TraceCallStore? calls, Dictionary<int, string?> classNames, int callNodeIndex)
    {
        if (!classNames.TryGetValue(callNodeIndex, out var className))
        {
            className = calls?.GetNode(callNodeIndex)?.Frame?.Function?.ClassName is { Length: > 0 } found ? found : null;

            classNames[callNodeIndex] = className;
        }

        return className;
    }
}
