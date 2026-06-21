using InternalsViewer.Replay.Events;
using InternalsViewer.Replay.Plans;

namespace InternalsViewer.Replay;

public sealed record QueryResult
{
    public required string SessionId { get; set; }

    public bool IsSuccess { get; set; }

    public List<EngineEvent> EngineEvents { get; set; } = [];

    public List<ExecutionPlan> ExecutionPlans { get; set; } = [];

    public string Message { get; set; } = string.Empty;

    public long RowCount { get; set; }
}