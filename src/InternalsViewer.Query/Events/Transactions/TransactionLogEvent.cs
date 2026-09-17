using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.TransactionLog;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Events.Transactions;

public sealed partial record TransactionLogEvent : RowIdentifierEngineEvent
{
    [EventProperty("Operation")]
    public LogOperation Operation { get; init; }

    public override string Description
    {
        get
        {
            if (PageAddress is not null)
            {
                return $"{Operation}/{Context} {PageAddress}";
            }

            return $"{Operation}/{Context}";
        }
    }

    public override string Name => "Transaction Log Record";

    [EventProperty("Context")]
    public LogContext Context { get; set; }

    [EventProperty("Allocation Unit Id")]
    public long AllocationUnitId { get; set; }

    [EventProperty("Transaction Id")]
    public int? TransactionId { get; set; }

    [EventProperty("Record Size", Type = EventPropertyType.Bytes)]
    public long LogRecordSize { get; set; }

    public LogRecord? LogRecord { get; set; }

    public override PageAddress? PageAddress => (LogRecord as PageLogRecord)?.PageAddress;

    public override RowIdentifier? RowIdentifier => (LogRecord as PageLogRecord)?.RowIdentifier;
}