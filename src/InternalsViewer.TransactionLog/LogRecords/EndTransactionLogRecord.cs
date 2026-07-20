namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// LOP_END_XACT log record
/// </summary>
public sealed record EndTransactionLogRecord : LogRecord
{
    /// <summary>
    /// Time the transaction committed or aborted
    /// </summary>
    /// <remarks>
    /// 8 byte datetime at offset 24
    ///
    /// Produced for both LOP_COMMIT_XACT and LOP_ABORT_XACT - the Operation property distinguishes the outcome.
    /// </remarks>
    public DateTime EndTime { get; set; }
}
