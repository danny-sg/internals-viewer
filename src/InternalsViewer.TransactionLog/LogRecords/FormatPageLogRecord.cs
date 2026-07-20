namespace InternalsViewer.Query.TransactionLog.LogRecords;

/// <summary>
/// LOP_FORMAT_PAGE log record
/// </summary>
/// <remarks>
/// Initialises a page's header when the page is newly allocated or reformatted. Index build format records also carry the full formatted
/// page contents as a variable element, which fn_dblog truncates at the [Log Record] varbinary(8000) cap.
/// </remarks>
public sealed record FormatPageLogRecord : PageLogRecord
{
    /// <summary>
    /// Page type the page is formatted as
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 48, matching the page header m_type values - 1 data, 2 index, 10 IAM etc.
    /// </remarks>
    public int PageType { get; set; }

    /// <summary>
    /// B-tree level of the formatted page
    /// </summary>
    /// <remarks>
    /// Single byte at offset 50. Zero for heap, leaf and IAM pages; 1 and above for index interior levels.
    /// </remarks>
    public int PageLevel { get; set; }

    /// <summary>
    /// Format option for the operation
    /// </summary>
    /// <remarks>
    /// Single byte at offset 51. Observed as 2 when formatting a newly allocated heap page and 0 otherwise.
    ///
    /// The individual values are undocumented.
    /// </remarks>
    public int FormatOption { get; set; }

    /// <summary>
    /// Page status flags recorded at format time
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 66. Observed as 4 when a heap page is reformatted by compaction and 0 for fresh formats.
    ///
    /// The individual bits are undocumented.
    /// </remarks>
    public int PageStat { get; set; }
}
