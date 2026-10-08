using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Events.BatchMode;

internal static class SegmentEliminateEventParser
{
    public static SegmentEliminateEvent Map(DatabaseSource? databaseSource, EventResult e) => new()
    {
        Name = e.Name,
        Timestamp = e.Timestamp,
        DatabaseId = e.GetDatabaseId(),
        RowGroupId = e.GetLong("rowgroup_id") ?? 0,
        HobtId = e.GetUlong("hobt_id") ?? 0,
        IsEliminatedByUniqueValueFilter = e.GetBool("eliminated_by_unique_value_filter") ?? false
    };
}
