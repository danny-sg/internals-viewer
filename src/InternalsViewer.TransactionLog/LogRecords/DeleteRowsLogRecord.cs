namespace InternalsViewer.Query.TransactionLog.LogRecords;

/// <summary>
/// LOP_DELETE_ROWS log record
/// </summary>
public sealed record DeleteRowsLogRecord : RowLogRecord
{
    /// <summary>
    /// Image of the row removed from the page
    /// </summary>
    /// <remarks>
    /// Variable element 0 - the complete row as it was stored on the page before deletion. Undo re-inserts it at SlotId to reverse the
    /// delete.
    ///
    /// Rows larger than the fn_dblog [Log Record] varbinary(8000) cap come back truncated.
    /// </remarks>
    public byte[] RowData { get; set; } = [];
}
