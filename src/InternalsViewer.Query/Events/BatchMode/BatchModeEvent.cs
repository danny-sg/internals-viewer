using InternalsViewer.Query.Events.Properties;

namespace InternalsViewer.Query.Events.BatchMode;

public sealed partial record BatchModeEvent : EngineEvent
{
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
}
