using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Events.BatchMode;

internal static class ColumnstoreFilterEventParser
{
    public static ColumnstoreFilterEvent Map(DatabaseSource? databaseSource, EventResult e) => new()
    {
        EventName = e.Name,
        Timestamp = e.Timestamp,
        DatabaseId = e.GetDatabaseId(),
        NodeId = (int)(e.GetLong("node_id") ?? e.GetLong("query_operator_node_id") ?? 0),
        ThreadId = (int)(e.GetLong("thread_id") ?? e.GetLong("query_thread_id") ?? 0),
        RowGroupId = e.GetLong("rowgroup_id"),
        ColumnId = (int?)e.GetLong("rowset_column_id"),
        InputRows = e.GetLong("input_rows") ?? 0,
        OutputRows = e.GetLong("output_rows") ?? 0,
        IsPure = e.GetBool("is_pure"),
        IsPrefiltered = e.GetBool("is_prefiltered")
    };
}
