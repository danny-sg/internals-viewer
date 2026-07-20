using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// Page scoped log record
/// </summary>
public abstract record PageLogRecord : LogRecord
{
    /// <summary>
    /// Page the operation applies to
    /// </summary>
    /// <remarks>
    /// Page id at offset 24 (4 bytes) and file id at offset 28 (2 bytes) of the record. For allocation records (SET_BITS, SET_FREE_SPACE)
    /// this is the bitmap or PFS page being written, not the data page whose state is being tracked.
    /// </remarks>
    public PageAddress PageAddress { get; set; }

    /// <summary>
    /// Slot index on the page
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 30. For row operations this is the target row's slot; for allocation bitmap records it is the slot of the record
    /// holding the bitmap; -1 for operations with no slot such as LOP_FORMAT_PAGE.
    /// </remarks>
    public int SlotId { get; set; }

    public RowIdentifier RowIdentifier => new(PageAddress, (ushort)SlotId);

    /// <summary>
    /// LSN the page's header held before this operation was applied
    /// </summary>
    /// <remarks>
    /// 10 bytes at offset 36. The page-scoped records touching one page form a chain - each record's PreviousPageLsn is the LSN the
    /// previous record stamped into the page header (m_lsn), so replay can verify it is applying to the exact page version the record
    /// expects and detect missing records.
    /// </remarks>
    public LogSequenceNumber PreviousPageLsn { get; set; }
}
