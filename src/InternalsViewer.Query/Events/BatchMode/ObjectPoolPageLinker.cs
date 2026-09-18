using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Events.BatchMode;

/// <summary>
/// Gives each object pool lookup the pages of the segment or dictionary it was for, from the columnstore page map
/// </summary>
/// <remarks>
/// A segment and a secondary dictionary are matched on rowgroup and column, so the lookup needs its rowgroup stamped
/// first. A primary dictionary is shared across rowgroups and is matched on column alone. The delete bitmap is not a LOB,
/// so its pages are the ones read from its own allocation unit. Column ids need no translation: the pool event and the
/// page map both use the columnstore's own column numbering, one ahead of the catalog's.
/// </remarks>
public static class ObjectPoolPageLinker
{
    public static void Link(IReadOnlyList<EngineEvent> events,
                            long hobtId,
                            IReadOnlyList<ColumnstorePageRead> reads,
                            AllocationUnit? deleteBitmapUnit = null)
    {
        var deleteBitmapPages = DeleteBitmapPages(events, deleteBitmapUnit);

        var pages = new Dictionary<(ColumnstoreReadType Type, int RowGroup, int Column), List<PageAddress>>();

        var secondaryDictionaryPages = new Dictionary<(int Column, int Dictionary), HashSet<PageAddress>>();

        foreach (var read in reads)
        {
            if (read is { ReadType: ColumnstoreReadType.Dictionary, RowGroupId: >= 0 })
            {
                if (!secondaryDictionaryPages.TryGetValue((read.ColumnId, read.DictionaryId), out var dictionaryList))
                {
                    dictionaryList = [];

                    secondaryDictionaryPages[(read.ColumnId, read.DictionaryId)] = dictionaryList;
                }

                dictionaryList.Add(read.PageAddress);

                continue;
            }

            var key = (read.ReadType, read.RowGroupId, read.ColumnId);

            if (!pages.TryGetValue(key, out var list))
            {
                list = [];

                pages[key] = list;
            }

            list.Add(read.PageAddress);
        }

        foreach (var engineEvent in events)
        {
            if (engineEvent is not ObjectPoolEvent pool || (long)pool.HobtId != hobtId)
            {
                continue;
            }

            if (pool.ObjectType == ColumnStoreObjectType.DeleteBitmap)
            {
                pool.Pages = deleteBitmapPages;
            }
            else if (pool.ObjectType == ColumnStoreObjectType.SecondaryDictionary)
            {
                pool.Pages = secondaryDictionaryPages.TryGetValue((pool.ColumnId, pool.PoolObjectId), out var dictionaryPages)
                    ? [.. dictionaryPages]
                    : [];
            }
            else if (KeyOf(pool) is { } key && pages.TryGetValue(key, out var list))
            {
                pool.Pages = list;
            }
        }
    }

    private static IReadOnlyList<PageAddress> DeleteBitmapPages(IReadOnlyList<EngineEvent> events, AllocationUnit? deleteBitmapUnit)
    {
        if (deleteBitmapUnit is null)
        {
            return [];
        }

        var pages = new HashSet<PageAddress>();

        foreach (var engineEvent in events)
        {
            if (engineEvent.AllocationUnit?.AllocationUnitId != deleteBitmapUnit.AllocationUnitId)
            {
                continue;
            }

            if (engineEvent is ReadEventGroup group)
            {
                pages.UnionWith(group.Pages);
            }
            else if (engineEvent is PageEngineEvent { PageAddress: { } address })
            {
                pages.Add(address);
            }
        }

        return [.. pages];
    }

    private static (ColumnstoreReadType Type, int RowGroup, int Column)? KeyOf(ObjectPoolEvent pool) => pool switch
    {
        { ObjectType: ColumnStoreObjectType.ColumnSegment, RowGroupId: { } rowGroup }
            => (ColumnstoreReadType.Segment, (int)rowGroup, pool.ColumnId),
        { ObjectType: ColumnStoreObjectType.PrimaryDictionary }
            => (ColumnstoreReadType.Dictionary, -1, pool.ColumnId),
        _ => null
    };
}
