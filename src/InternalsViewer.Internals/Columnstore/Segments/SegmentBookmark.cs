namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Entry point into the RLE array for one bookmark interval
/// </summary>
public readonly record struct SegmentBookmark(int Position, int EndRow)
{
    /// <summary>
    /// Position is held in four byte units, so the entry it lands on depends on the entry width
    /// </summary>
    /// <summary>
    /// Whether the position is the sentinel a store by value segment writes rather than a place in the RLE array
    /// </summary>
    public bool IsSentinel => Position == unchecked((int)0x80000000);

    public int GetRleEntryIndex(int entryBytes) => Position * 4 / entryBytes;
}
