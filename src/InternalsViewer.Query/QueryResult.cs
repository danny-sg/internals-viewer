using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;

namespace InternalsViewer.Query;

public sealed record QueryResult
{
    public required string SessionId { get; set; }

    public bool IsSuccess { get; set; }

    public List<EngineEvent> EngineEvents { get; set; } = [];

    public List<ExecutionPlan> ExecutionPlans { get; set; } = [];

    public string Message { get; set; } = string.Empty;

    public long RowCount { get; set; }
}