namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Layout the segment uses for its value stream
/// </summary>
public enum SegmentStructureType
{
    Unknown = 0,

    /// <summary>
    /// Bookmark array, RLE array and bit pack array
    /// </summary>
    RunLength = 3,

    /// <summary>
    /// Used by the store by value encodings, where values are held outside the RLE array
    /// </summary>
    StoreByValue = 7
}
