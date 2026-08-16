namespace InternalsViewer.Execution.AccessPaths.Memory;

/// <summary>
/// What an operator holding rows in memory is using, split so the row images and the structure around them can be told apart
/// </summary>
/// <remarks>
/// Kept as two figures because the comparison with the grant SQL Server reports is the point of measuring it. A difference that grows
/// with the row count says the row image is modelled wrongly, one that tracks the bucket count says the structure is.
/// </remarks>
public readonly record struct BufferMemory(long RowBytes, long OverheadBytes)
{
    public long TotalBytes => RowBytes + OverheadBytes;

    public double TotalKb => TotalBytes / 1024D;

    /// <summary>
    /// The total taken up to whole pages, which is how a workspace is allocated and how a grant is reported
    /// </summary>
    /// <remarks>
    /// The model is an approximation to the byte, so reporting it to the byte reads as a precision it does not have. Rounding it the way
    /// the engine allocates says both what is held and that the figure is granular.
    /// </remarks>
    public long PagedBytes => TotalBytes == 0
        ? 0
        : (TotalBytes + RowMemory.PageBytes - 1) / RowMemory.PageBytes * RowMemory.PageBytes;

    public double PagedKb => PagedBytes / 1024D;

}
