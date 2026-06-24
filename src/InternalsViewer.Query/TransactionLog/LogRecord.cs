using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.TransactionLog;

public sealed record LogRecord
{
    public required LogSequenceNumber Lsn { get; init; }
    
    public required LogSequenceNumber PreviousLsn { get; init; }
    
    public string LogTransactionId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;
    
    public string Context { get; set; } = string.Empty;

    public long? AllocationUnitId { get; set; }
    
    public long? PartitionId { get; set; }

    public PageAddress? PageAddress { get; set; }

    public int? SlotId { get; set; }

    public byte[] RowLogContents0 { get; set; } = [];

    public byte[] RowLogContents1 { get; set; } = [];
    
    public byte[] RowLogContents2 { get; set; } = [];

    public int? TransactionId { get; set; }

    public DateTime? ApproximateTime { get; set; }

    public int? SequenceId { get; set; }
}