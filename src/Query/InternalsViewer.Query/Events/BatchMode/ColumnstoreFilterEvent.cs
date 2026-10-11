using InternalsViewer.Query.Events.Properties;

namespace InternalsViewer.Query.Events.BatchMode;

public sealed partial record ColumnstoreFilterEvent : EngineEvent
{
    public const string BatchFilter = "query_execution_batch_filter";

    public const string ExpressionFilterApply = "column_store_expression_filter_apply";

    [EventProperty("Event")]
    public string EventName { get; init; } = string.Empty;

    [EventProperty("Node")]
    public int NodeId { get; init; }

    [EventProperty("Row Group")]
    public long? RowGroupId { get; init; }

    [EventProperty("Column")]
    public int? ColumnId { get; init; }

    [EventProperty("Input Rows", Type = EventPropertyType.Number)]
    public long InputRows { get; init; }

    [EventProperty("Output Rows", Type = EventPropertyType.Number)]
    public long OutputRows { get; init; }

    [EventProperty("Pure")]
    public bool? IsPure { get; init; }

    [EventProperty("Prefiltered")]
    public bool? IsPrefiltered { get; init; }

    public bool IsBatchFilter => EventName == BatchFilter;

    public override string Name => IsBatchFilter ? "Batch Filter" : "Bitmap Filter";

    public override string Description => $"{Name} ({OutputRows:N0} of {InputRows:N0} rows)";
}
