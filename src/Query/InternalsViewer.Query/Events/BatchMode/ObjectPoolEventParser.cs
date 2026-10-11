using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Events.BatchMode;

internal static class ObjectPoolEventParser
{
    public static ObjectPoolEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        var objectType = (ColumnStoreObjectType)(e.GetInt("object_type") ?? 0);

        var objectId = (int)(e.GetUlong("object_id") ?? 0);

        var hobtId = e.GetUlong("hobt_id") ?? 0;

        return new ObjectPoolEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            IsHit = e.Name.EndsWith("_hit", StringComparison.Ordinal),
            ObjectType = objectType,
            HobtId = hobtId,
            AllocationUnit = databaseSource?.FindHobtIdAllocationUnit((long)hobtId),
            ColumnId = (int)(e.GetUlong("column_id") ?? 0),
            PoolObjectId = objectId,
            RowGroupId = IsRowGroupObject(objectType) ? objectId : null
        };
    }

    private static bool IsRowGroupObject(ColumnStoreObjectType objectType)
        => objectType is ColumnStoreObjectType.ColumnSegment or ColumnStoreObjectType.DeleteBitmap;
}
