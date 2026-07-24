namespace InternalsViewer.Connection.BackupFile.Index;

/// <summary>
/// Compact representation of a consecutive run of pages in a SQL Server backup file
/// </summary>
/// <remarks>
/// Represents a consecutive run of pages in a SQL Server backup file from start page id to start page id + page count - 1
///
/// From the information in the run any offset can be derived for pages in the range.
///
/// A run belongs to a single stripe (file) - byte offsets are not contiguous across files, so a run never spans
/// stripes.
/// </remarks>
internal readonly record struct PageRun(short FileId, int StartPageId, int PageCount, int StripeIndex, long StartOffset)
{
    public int EndPageId => StartPageId + PageCount - 1;
}
