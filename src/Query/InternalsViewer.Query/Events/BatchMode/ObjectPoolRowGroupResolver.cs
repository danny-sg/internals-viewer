using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Events.BatchMode;

public static class ObjectPoolRowGroupResolver
{
    public static void Resolve(IReadOnlyList<EngineEvent> events,
                               long hobtId,
                               IEnumerable<(int ColumnId, int DictionaryId, int RowGroupId)> secondaryDictionaries)
    {
        var rowGroups = secondaryDictionaries.GroupBy(d => (d.ColumnId, d.DictionaryId))
                                             .ToDictionary(g => g.Key, g => g.Select(d => d.RowGroupId).Distinct().ToList());

        foreach (var engineEvent in events)
        {
            if (engineEvent is not ObjectPoolEvent { ObjectType: ColumnStoreObjectType.SecondaryDictionary } pool
                || (long)pool.HobtId != hobtId)
            {
                continue;
            }

            pool.RowGroupId = rowGroups.TryGetValue((pool.ColumnId, pool.PoolObjectId), out var users) && users.Count == 1
                ? users[0]
                : null;
        }
    }
}
