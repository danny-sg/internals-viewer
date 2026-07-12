using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Results;

namespace InternalsViewer.Query;

public sealed record QueryResult
{
    public required string SessionId { get; set; }

    public bool IsSuccess { get; set; }

    public List<EngineEvent> EngineEvents { get; set; } = [];

    public List<ExecutionPlan> ExecutionPlans { get; set; } = [];

    /// <summary>
    /// Merged query call stack
    /// </summary>
    public Callstack.CallStackTree? CallStack { get; set; }

    public List<QueryResultSet> ResultSets { get; set; } = [];

    public string Message { get; set; } = string.Empty;

    public long RowCount { get; set; }

    /// <summary>
    /// The query's time window (microseconds) when cropped to the executed query, otherwise null — for the timeline axis
    /// </summary>
    public long? CropStartUs { get; set; }

    public long? CropEndUs { get; set; }
}