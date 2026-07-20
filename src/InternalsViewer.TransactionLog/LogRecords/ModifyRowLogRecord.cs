namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// LOP_MODIFY_ROW log record
/// </summary>
public sealed record ModifyRowLogRecord : RowLogRecord
{
    /// <summary>
    /// Byte offset within the row where the modification starts
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 56. Together with ModifySize this defines the byte range of the row being replaced - the modification is a single
    /// contiguous splice rather than per-column changes (multi-column updates log LOP_MODIFY_COLUMNS instead).
    /// </remarks>
    public int OffsetInRow { get; set; }

    /// <summary>
    /// Number of bytes replaced in the row
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 58. The length of the range currently on the page that the modification replaces, so it pairs with AfterData's
    /// length changing the row size when the before and after images differ in length.
    /// </remarks>
    public int ModifySize { get; set; }

    /// <summary>
    /// Bytes removed from the row (before image)
    /// </summary>
    /// <remarks>
    /// Variable element 0. Used by undo to restore the original contents at OffsetInRow. Empty on COMPENSATION records - the undo of an
    /// undo is never needed, so compensation records log no before image.
    /// </remarks>
    public byte[] BeforeData { get; set; } = [];

    /// <summary>
    /// Bytes written to the row (after image)
    /// </summary>
    /// <remarks>
    /// Variable element 1. The redo payload spliced into the row at OffsetInRow. On COMPENSATION records this holds the original bytes
    /// being restored by the rollback.
    /// </remarks>
    public byte[] AfterData { get; set; } = [];
}
