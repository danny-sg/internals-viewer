using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Events.BatchMode;

internal static class BatchModeEventParser
{
    public static BatchModeEvent? Map(DatabaseSource? databaseSource, EventResult e) => e.Name switch
    {
        "query_execution_batch_hash_aggregation_finished" => new BatchModeEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            NodeId = e.GetInt("query_operator_node_id") ?? 0,
            IsFastComparisonUsed = e.GetBool("fast_comparison_used"),
            IsLocalAggregationUsed = e.GetBool("local_aggregation_used")
        },
        "query_execution_batch_filter" => new BatchModeEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            NodeId = e.GetInt("query_operator_node_id") ?? 0,
            IsPrefiltered = e.GetBool("is_prefiltered")
        },
        "query_execution_batch_global_string_dictionary" => new BatchModeEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            NodeId = e.GetInt("query_operator_node_id") ?? 0,
            IsGlobalDictionaryUsed = e.GetBool("is_dictionary_used"),
            GlobalDictionaryKeyColumns = NullIfEmpty(e.GetString("key_column_ids"))
        },
        _ => null
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
