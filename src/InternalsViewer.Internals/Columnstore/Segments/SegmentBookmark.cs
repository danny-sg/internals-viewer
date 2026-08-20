namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Entry point into the RLE array for one bookmark interval
/// </summary>
public readonly record struct SegmentBookmark(int Position, int EndRow)
{
    /// <summary>
    /// Position is held in four byte units, so the entry it lands on depends on the entry width
    /// </summary>
    public int GetRleEntryIndex(int entryBytes) => Position * 4 / entryBytes;
}
