using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Events.BatchMode;

internal static class ColumnStoreScanEventParser
{
    public static ColumnStoreScanEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        var rowGroupId = e.GetLong("rowgroup_id");

        return new ColumnStoreScanEvent
        {
            Name = e.Name,
            EventName = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            RowGroupId = rowGroupId,
            HobtId = e.GetUlong("hobt_id") ?? 0,
            Summary = Summarise(e, rowGroupId)
        };
    }

    private static string Summarise(EventResult e, long? rowGroupId) => e.Name switch
    {
        "column_store_rowgroup_read_issued" => $"Row Group {rowGroupId}",
        "column_store_rowgroup_readahead_issued"
            => $"Row Group {rowGroupId}, {e.GetUlong("actual_read_ahead_in_bytes") ?? 0:N0} bytes read ahead",
        "column_store_fast_string_equals" => $"Parameter {e.GetUlong("param_id") ?? 0}",
        "query_execution_wait_syncpoint"
            => $"{e.GetString("wait_type")} {(e.GetBool("start_wait") == true ? "Start" : "End")}",
        _ => rowGroupId is { } id ? $"Row Group {id}" : string.Empty
    };
}
