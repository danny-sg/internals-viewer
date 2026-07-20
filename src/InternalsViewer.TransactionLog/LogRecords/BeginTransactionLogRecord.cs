namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// LOP_BEGIN_XACT log record
/// </summary>
public sealed record BeginTransactionLogRecord : LogRecord
{
    /// <summary>
    /// Time the transaction began
    /// </summary>
    /// <remarks>
    /// 8 byte datetime at offset 40 - DATETIME encoding
    /// </remarks>
    public DateTime BeginTime { get; set; }

    /// <summary>
    /// Name of the transaction
    /// </summary>
    /// <remarks>
    /// Variable element 0, UTF-16 with no terminator.
    ///
    /// The name given to BEGIN TRANSACTION, or a system-assigned name such as user_transaction, INSERT or AllocPages for implicit and
    /// system transactions.
    /// </remarks>
    public string TransactionName { get; set; } = string.Empty;

    /// <summary>
    /// Security identifier (SID) of the login that started the transaction
    /// </summary>
    /// <remarks>
    /// Variable element 1 - the raw Windows or SQL login SID bytes
    /// </remarks>
    public byte[] TransactionSid { get; set; } = [];
}
