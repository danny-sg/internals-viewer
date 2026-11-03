using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed class StorageIdMap
{
    private const ulong SmallestId = 1UL << 32;

    private StorageIdMap(Dictionary<ulong, StorageId> ids)
    {
        Ids = ids;
    }

    public static StorageIdMap Empty { get; } = new([]);

    private Dictionary<ulong, StorageId> Ids { get; }

    public static StorageIdMap Build(IEnumerable<AllocationUnit> allocationUnits)
    {
        var named = allocationUnits.Where(a => a.TableName.Length > 0).ToList();

        var ids = named.Where(a => a.AllocationUnitType == AllocationUnitType.InRowData)
                       .DistinctBy(a => a.PartitionId)
                       .ToDictionary(a => (ulong)a.PartitionId, a => new StorageId(StorageIdType.HoBt, a));

        foreach (var allocationUnit in named)
        {
            ids.TryAdd((ulong)allocationUnit.AllocationUnitId, new StorageId(StorageIdType.AllocationUnit, allocationUnit));
        }

        return new StorageIdMap(ids);
    }

    public StorageId? GetHoBt(ulong hobtId)
        => Ids.TryGetValue(hobtId, out var id) && id.Type == StorageIdType.HoBt ? id : null;

    public StorageId? GetStorageId(ulong value) => value >= SmallestId ? Ids.GetValueOrDefault(value) : null;
}
