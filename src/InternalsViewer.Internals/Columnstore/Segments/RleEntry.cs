namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// One run in a segment RLE array
/// </summary>
/// <remarks>
/// There are three types of runs:
///
///     Value greater than 0 - Repeat: - Start of repeated run for the Count length
///     Value less than 0    - Read:  - Value from source is read forward for Count items
///     Value equal to 0     - Terminator
///
/// Bit Pack
/// --------
///
/// Repeat:
///   Value is an in-place value
///
/// Read:
///   -Value - 1 is the index of the first bit packed value the run covers
///
/// Variable Length Data
///
/// Repeat:
///     Value is the page slot of the value to repeat, which is a 15 bit page index and a 14 bit slot index
///
/// Read:
///     Value is the page slot of the first value to read, which is a 15 bit page index and a 14 bit slot index 
/// 
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
