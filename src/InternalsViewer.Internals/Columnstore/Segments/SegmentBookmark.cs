namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Entry point into the RLE array for one bookmark interval
/// </summary>
public readonly record struct SegmentBookmark(int Position, int EndRow)
{
    /// <summary>
    /// Position is held in four byte units and RLE entries are eight bytes
    /// </summary>
    public int RleEntryIndex => Position / 2;
}
