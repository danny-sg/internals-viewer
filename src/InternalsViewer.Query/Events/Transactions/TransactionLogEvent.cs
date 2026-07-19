using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.TransactionLog;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Events.Transactions;

public sealed record TransactionLogEvent : RowIdentifierEngineEvent
{
    public LogOperation Operation { get; init; }

    public override string Description => $"{Operation}/{Context}";

    public LogContext Context { get; set; }

    public long AllocationUnitId { get; set; }

    public int? TransactionId { get; set; }

    public long LogRecordSize { get; set; }

    public LogRecord? LogRecord { get; set; }

    public override PageAddress? PageAddress => (LogRecord as PageLogRecord)?.PageAddress;

    public override RowIdentifier? RowIdentifier => (LogRecord as PageLogRecord)?.RowIdentifier;
}