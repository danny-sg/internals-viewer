namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// One run in a segment RLE array
/// </summary>
/// <remarks>
/// There are three types of runs:
///
///     Value greater than 0 - Value Run
///     Value less than 0    - Either the Bit Pack Run or the VLD Page Slot depending on the Segment Structure Type
///     Value equal to 0     - Terminator
/// </remarks>
public readonly record struct RleEntry(long Value, int Count, bool IsVariableLengthData = false)
{
    public bool IsValue => Value >= 0;

    /// <summary>
    /// Where in the value store the run reads, which only a store by value segment addresses
    /// </summary>
    public SegmentPageSlot? PageSlot => IsVariableLengthData && !IsTerminator
                                        ? new SegmentPageSlot((int)(Value & 0x7FFF), (int)((Value & 0x3FFF8000) >> 15))
                                        : null;

    public bool IsTerminator => Value == 0 && Count == 0;

    /// <summary>
    /// Index of the first bit packed value the run covers
    /// </summary>
    public int BitpackIndex => (int)(-Value - 1);
}
