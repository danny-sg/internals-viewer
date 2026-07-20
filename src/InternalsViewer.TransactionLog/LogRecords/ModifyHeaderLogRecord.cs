namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// LOP_MODIFY_HEADER log record
/// </summary>
/// <remarks>
/// Replaces a field in the page header. Unlike LOP_MODIFY_ROW the offset is into the 96 byte header rather than a
/// slotted row, and the before/after images are the variable elements 0 and 1 (the fixed body's Modify Size is not
/// the change length here - the after image length is).
/// </remarks>
public sealed record ModifyHeaderLogRecord : PageLogRecord
{
    /// <summary>
    /// Byte offset of the modified field within the page header
    /// </summary>
    public int HeaderOffset { get; set; }

    /// <summary>
    /// Header bytes before the modification (before image)
    /// </summary>
    public byte[] BeforeData { get; set; } = [];

    /// <summary>
    /// Header bytes written by the modification (after image)
    /// </summary>
    public byte[] AfterData { get; set; } = [];
}
