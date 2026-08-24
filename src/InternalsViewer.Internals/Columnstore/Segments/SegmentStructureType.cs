namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Layout the segment uses for its value stream
/// </summary>
public enum SegmentStructureType
{
    Unknown = 0,

    /// <summary>
    /// Runs address the bit pack array
    /// </summary>
    BitPack = 3,

    /// <summary>
    /// Runs address the variable length data store
    /// </summary>
    VariableLengthData = 7
}
