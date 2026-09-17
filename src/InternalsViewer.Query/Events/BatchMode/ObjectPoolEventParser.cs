using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Events.BatchMode;

internal static class ObjectPoolEventParser
{
    public static ObjectPoolEvent Map(DatabaseSource? databaseSource, EventResult e) => new()
    {
        Name = e.Name,
        Timestamp = e.Timestamp,
        DatabaseId = e.GetDatabaseId(),
        IsHit = e.Name.EndsWith("_hit", StringComparison.Ordinal),
        ObjectType = (ColumnStoreObjectType)(e.GetInt("object_type") ?? 0),
        HobtId = e.GetUlong("hobt_id") ?? 0,
        ColumnId = (int)(e.GetUlong("column_id") ?? 0),
        PoolObjectId = (int)(e.GetUlong("object_id") ?? 0)
    };
}
