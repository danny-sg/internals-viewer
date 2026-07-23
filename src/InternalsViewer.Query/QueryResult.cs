using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.Query.Results;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query;

public sealed record QueryResult
{
    public required string SessionId { get; set; }

    public bool IsSuccess { get; set; }

    public List<EngineEvent> EngineEvents { get; set; } = [];

    public List<ExecutionPlan> ExecutionPlans { get; set; } = [];

    public CallStackTree? CallStackTree { get; set; }

    public List<QueryResultSet> ResultSets { get; set; } = [];

    public string Message { get; set; } = string.Empty;

    public long RowCount { get; set; }

    public long? CropStartUs { get; set; }

    public long? CropEndUs { get; set; }

    public List<LogRecord> LogRecords { get; set; } = [];
}