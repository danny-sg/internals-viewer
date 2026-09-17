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
            Summary = Summarise(e, rowGroupId),
            TimeToGenerateUs = e.GetDouble("time_to_generate") is { } seconds ? (long)(seconds * 1_000_000) : null
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
        "large_cache_caching_decision"
            => $"{(e.GetBool("decision") == true ? "Cached" : "Not Cached")}, {e.GetUlong("size_in_pages") ?? 0:N0} pages",
        _ => rowGroupId is { } id ? $"Row Group {id}" : string.Empty
    };
}
