using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Events.BatchMode;

internal static class BatchModeEventParser
{
    public static BatchModeEvent? Map(DatabaseSource? databaseSource, EventResult e) => e.Name switch
    {
        "query_execution_batch_hash_aggregation_finished" => new BatchModeEvent
        {
            EventName = e.Name,
            Timestamp = e.Timestamp,
            NodeId = e.GetInt("query_operator_node_id") ?? 0,
            IsFastComparisonUsed = e.GetBool("fast_comparison_used"),
            IsLocalAggregationUsed = e.GetBool("local_aggregation_used")
        },
        "query_execution_batch_global_string_dictionary" => new BatchModeEvent
        {
            EventName = e.Name,
            Timestamp = e.Timestamp,
            NodeId = e.GetInt("query_operator_node_id") ?? 0,
            IsGlobalDictionaryUsed = e.GetBool("is_dictionary_used"),
            GlobalDictionaryKeyColumns = NullIfEmpty(e.GetString("key_column_ids"))
        },
        "query_execution_push_down_aggregate" => new BatchModeEvent
        {
            EventName = e.Name,
            Timestamp = e.Timestamp,
            NodeId = e.GetInt("query_operator_node_id") ?? 0,
            AggregationCount = e.GetInt("aggregation_count"),
            GroupByCount = e.GetInt("group_by_count")
        },
        "query_execution_dynamic_push_down_statistics" => new BatchModeEvent
        {
            EventName = e.Name,
            Timestamp = e.Timestamp,
            NodeId = e.GetInt("node_id") ?? 0,
            ThreadId = e.GetInt("thread_id") ?? 0,
            RowGroupId = e.GetLong("rowgroup_id"),
            RowsPushedDown = e.GetLong("rows_pushed_down_in_thread"),
            RowsNotPushedEncoding = e.GetLong("rows_not_pushed_down_due_to_encoding"),
            RowsNotPushedOverflow = e.GetLong("rows_not_pushed_down_due_to_possible_overflow"),
            RowsNotPushedDisabled = e.GetLong("rows_not_pushed_down_due_to_pushdown_disabled")
        },
        _ => null
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
