namespace InternalsViewer.Query.TransactionLog.LogRecords;

/// <summary>
/// Single contiguous byte region modified by a LOP_MODIFY_COLUMNS log record
/// </summary>
/// <remarks>
/// A region is a byte splice, not a column - it can start at the row's variable length offset array entry and span
/// unchanged columns that sit between two modified areas, and for fixed length columns it covers only the bytes
/// that actually changed.
/// </remarks>
public sealed record ColumnModification
{
    /// <summary>
    /// Byte offset of the region in the row before the modification
    /// </summary>
    public int BeforeOffset { get; set; }

    /// <summary>
    /// Byte offset of the region in the row after the modification
    /// </summary>
    public int AfterOffset { get; set; }

    /// <summary>
    /// Length of the region before the modification
    /// </summary>
    /// <remarks>
    /// From the record's element 1 length array, so it is authoritative even when BeforeData is absent
    /// (COMPENSATION records) or truncated by the fn_dblog varbinary(8000) cap
    /// </remarks>
    public int BeforeLength { get; set; }

    /// <summary>
    /// Bytes removed from the region (before image)
    /// </summary>
    public byte[] BeforeData { get; set; } = [];

    /// <summary>
    /// Bytes written to the region (after image)
    /// </summary>
    public byte[] AfterData { get; set; } = [];
}
