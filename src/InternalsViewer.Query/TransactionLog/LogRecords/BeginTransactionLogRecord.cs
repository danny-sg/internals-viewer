namespace InternalsViewer.Query.TransactionLog.LogRecords;

/// <summary>
/// LOP_BEGIN_XACT log record
/// </summary>
public sealed record BeginTransactionLogRecord : LogRecord
{
    /// <summary>
    /// Time the transaction began
    /// </summary>
    /// <remarks>
    /// 8 byte datetime at offset 40 - low 4 bytes are 1/300 second ticks since midnight, high 4 bytes are days since 1900-01-01, so
    /// precision is 3.33ms (the same encoding as the on-page datetime type).
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
    /// Security identifier of the login that started the transaction
    /// </summary>
    /// <remarks>
    /// Variable element 1 - the raw Windows or SQL login SID bytes, as surfaced in the fn_dblog [Transaction SID] column.
    /// </remarks>
    public byte[] TransactionSid { get; set; } = [];
}
