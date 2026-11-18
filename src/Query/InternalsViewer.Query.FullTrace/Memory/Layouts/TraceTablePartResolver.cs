using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

public sealed class TraceTablePartResolver
{
    private const int SampleInterval = 50;

    private TraceTablePartResolver(Dictionary<int, TraceHashPartType> hashParts, Dictionary<int, TraceSortPartType> sortParts)
    {
        HashParts = hashParts;

        SortParts = sortParts;
    }

    public static TraceTablePartResolver Empty { get; } = new([], []);

    private Dictionary<int, TraceHashPartType> HashParts { get; }

    private Dictionary<int, TraceSortPartType> SortParts { get; }

    public TraceHashPartType? GetHashPart(int block) => HashParts.TryGetValue(block, out var type) ? type : null;

    public TraceSortPartType? GetSortPart(int block) => SortParts.TryGetValue(block, out var type) ? type : null;

    public static TraceTablePartResolver Build(MemoryHistoryStore store, AddressMap addressMap, TraceCallStore calls)
    {
        var hashParts = new Dictionary<int, TraceHashPartType>();

        var sortParts = new Dictionary<int, TraceSortPartType>();

        var groups = TraceHashTableReader.GetGroups(calls).ToHashSet();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var info = store.Blocks[block];

            if (groups.Contains(info.Address))
            {
                foreach (var position in GetPositions(store, block))
                {
                    if (TraceHashTableReader.Read(store, info.Address, position) is not { } table)
                    {
                        continue;
                    }

                    foreach (var part in table.GetParts())
                    {
                        if (FindBlock(store, part.Address, position) is { } found)
                        {
                            hashParts.TryAdd(found, part.Type);
                        }
                    }
                }
            }
            else if (addressMap.GetLabel(info.Address, info.FromPosition, info.ToPosition) == TraceSortTableReader.IteratorClass)
            {
                var pages = TraceSortTableReader.GetPages(store, addressMap, info.Address);

                foreach (var position in GetPositions(store, block))
                {
                    if (TraceSortTableReader.Read(store, pages, info.Address, position) is not { } table)
                    {
                        continue;
                    }

                    foreach (var part in table.GetParts())
                    {
                        if (FindBlock(store, part.Address, position) is { } found)
                        {
                            sortParts.TryAdd(found, part.Type);
                        }
                    }
                }
            }
        }

        return new TraceTablePartResolver(hashParts, sortParts);
    }

    private static IEnumerable<TracePosition> GetPositions(MemoryHistoryStore store, int block)
    {
        var writes = store.GetWrites(block);

        for (var index = 0; index < writes.Count; index += SampleInterval)
        {
            yield return writes[index].Position;
        }

        if (writes.Count > 0)
        {
            yield return writes[^1].Position;
        }
    }

    private static int? FindBlock(MemoryHistoryStore store, ulong address, TracePosition position)
        => address != 0 && store.GetBlock(address, position) is >= 0 and var block && store.Blocks[block].Address == address
            ? block
            : null;
}
