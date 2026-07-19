namespace InternalsViewer.Query.TransactionLog.LogRecords;

/// <summary>
/// LOP_INSERT_ROWS log record
/// </summary>
public sealed record InsertRowsLogRecord : RowLogRecord
{
    /// <summary>
    /// Image of the row inserted onto the page
    /// </summary>
    /// <remarks>
    /// Variable element 0 - the complete row as stored on the page, including the row header and null bitmap.
    ///
    /// - Redo re-inserts it at SlotId
    /// - Undo removes the row at SlotId, so no separate before image is needed.
    ///
    /// Rows larger than the fn_dblog [Log Record] varbinary(8000) cap come back truncated.
    /// </remarks>
    public byte[] RowData { get; set; } = [];
}
