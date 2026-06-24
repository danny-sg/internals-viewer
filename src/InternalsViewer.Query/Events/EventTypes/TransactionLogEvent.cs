using InternalsViewer.Query.TransactionLog;

namespace InternalsViewer.Query.Events.EventTypes;

public sealed record TransactionLogEvent : EngineEvent
{
    public LogOperation Operation { get; init; }

    public override string Description => Operation.ToString();

    public LogContext Context { get; set; }

    public long AllocationUnitId { get; set; }
}