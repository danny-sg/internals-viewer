namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// LOP_SET_FREE_SPACE log record
/// </summary>
/// <remarks>
/// Log record setting a PFS byte to a new value
/// </remarks>
public sealed record SetFreeSpaceLogRecord : PageLogRecord
{
    /// <summary>
    /// Offset of the target page within the PFS interval
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 48.
    ///
    /// PageAddress is the PFS page being written. The page whose PFS byte changes is PageAddress.PageId + PageOffset (each PFS page tracks
    /// the following 8088 pages).
    /// </remarks>
    public int PageOffset { get; set; }

    /// <summary>
    /// PFS byte value after the change
    /// </summary>
    /// <remarks>
    /// Single byte at offset 50
    /// </remarks>
    public byte NewValue { get; set; }

    /// <summary>
    /// PFS byte value before the change
    /// </summary>
    /// <remarks>
    /// Single byte at offset 51
    ///
    /// PFS changes are logged outside the user transaction (transaction id zero), so they are not undone by rollback - the old value
    /// supports recovery reconstructing PFS state instead.
    /// </remarks>
    public byte OldValue { get; set; }
}
