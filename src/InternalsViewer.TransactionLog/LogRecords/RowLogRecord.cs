namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// Row scoped log record
/// </summary>
public abstract record RowLogRecord : PageLogRecord
{
    /// <summary>
    /// Partition (HoBt) id of the object the row belongs to
    /// </summary>
    /// <remarks>
    /// 8 bytes at offset 48. This is the value fn_dblog derives its AllocUnitId / AllocUnitName columns from via metadata lookup - the
    /// record itself only stores the partition id (it matches the HoBt id in the record's lock information).
    /// </remarks>
    public long PartitionId { get; set; }

    /// <summary>
    /// Number of variable length elements in the record
    /// </summary>
    /// <remarks>
    /// The variable section starts at the fixed length boundary: a 2 byte element count, then that many 2 byte lengths, then the element
    /// data with each element aligned to a 4 byte boundary (padding bytes are uninitialised memory, not zeros).
    /// </remarks>
    public int ElementCount { get; set; }
}
