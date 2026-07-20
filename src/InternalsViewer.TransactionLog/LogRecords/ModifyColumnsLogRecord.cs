namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// LOP_MODIFY_COLUMNS log record
/// </summary>
/// <remarks>
/// Logs an update touching multiple non-adjacent byte regions of a row as a list of splices, where LOP_MODIFY_ROW would log a single
/// contiguous splice.
///
/// Variable elements:
///     - Element 0 = (before offset, after offset) pairs per region
///     - Element 1 = before length per region
///     - Element 2 = unused
///     - Element 3 = lock information
///
/// After the variable elements a (before data, after data) element pair per region
/// </remarks>
public sealed record ModifyColumnsLogRecord : RowLogRecord
{
    /// <summary>
    /// Modified byte regions in row order
    /// </summary>
    /// <remarks>
    /// One entry per region - the region count is element 0's length / 4. Each region pairs its offsets from element 0 with its before and
    /// after images from the per-region data elements.
    /// </remarks>
    public IReadOnlyList<ColumnModification> Modifications { get; set; } = [];
}
