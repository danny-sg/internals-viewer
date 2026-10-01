using InternalsViewer.Query.Events.Properties;

namespace InternalsViewer.Query.Events.BatchMode;

public sealed partial record BatchModeEvent : EngineEvent
{
    [EventProperty("Event")]
    public string EventName { get; init; } = string.Empty;

    [EventProperty("Node")]
    public int NodeId { get; init; }

    [EventProperty("Fast Comparison")]
    public bool? IsFastComparisonUsed { get; init; }

    [EventProperty("Local Aggregation")]
    public bool? IsLocalAggregationUsed { get; init; }

    [EventProperty("Global Dictionary")]
    public bool? IsGlobalDictionaryUsed { get; init; }

    [EventProperty("Global Dictionary Key Columns")]
    public string? GlobalDictionaryKeyColumns { get; init; }

    [EventProperty("Aggregates", Type = EventPropertyType.Number)]
    public int? AggregationCount { get; init; }

    [EventProperty("Group By Columns", Type = EventPropertyType.Number)]
    public int? GroupByCount { get; init; }

    [EventProperty("Row Group")]
    public long? RowGroupId { get; init; }

    [EventProperty("Rows Pushed Down", Type = EventPropertyType.Number)]
    public long? RowsPushedDown { get; init; }

    [EventProperty("Rows Not Pushed (Encoding)", Type = EventPropertyType.Number)]
    public long? RowsNotPushedEncoding { get; init; }

    [EventProperty("Rows Not Pushed (Overflow)", Type = EventPropertyType.Number)]
    public long? RowsNotPushedOverflow { get; init; }

    [EventProperty("Rows Not Pushed (Disabled)", Type = EventPropertyType.Number)]
    public long? RowsNotPushedDisabled { get; init; }

    public override string Name => EventName switch
    {
        "query_execution_batch_hash_aggregation_finished" => "Hash Aggregation Finished",
        "query_execution_batch_global_string_dictionary" => "Global String Dictionary",
        "query_execution_push_down_aggregate" => "Aggregate Pushdown",
        "query_execution_dynamic_push_down_statistics" => "Aggregate Pushdown Statistics",
        _ => EventName
    };
}
