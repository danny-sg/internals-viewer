namespace InternalsViewer.Query.TransactionLog.LogRecords;

/// <summary>
/// LOP_MODIFY_COLUMNS log record
/// </summary>
/// <remarks>
/// Logs an update touching multiple non-adjacent byte regions of a row as a list of splices, where LOP_MODIFY_ROW
/// would log a single contiguous splice. The variable elements are: element 0 = (before offset, after offset)
/// pairs per region, element 1 = before length per region, element 2 = unused, element 3 = lock information, then
/// a (before data, after data) element pair per region. The fn_dblog [RowLog Contents 4] and [RowLog Contents 5]
/// columns show the raw record tail rather than these per-region elements.
/// </remarks>
public sealed record ModifyColumnsLogRecord : RowLogRecord
{
    /// <summary>
    /// Modified byte regions in row order
    /// </summary>
    /// <remarks>
    /// One entry per region - the region count is element 0's length / 4. Each region pairs its offsets from
    /// element 0 with its before and after images from the per-region data elements.
    /// </remarks>
    public IReadOnlyList<ColumnModification> Modifications { get; set; } = [];
}
