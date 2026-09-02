namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// One run in a Segment RLE array
/// </summary>
/// <remarks>
/// There are three types of runs:
///
///     Read, Repeat, and Terminator
///
/// Value is either 4 bytes or 8 bytes, depending on the data in the segment.
///
/// Run Type Bit Flags
/// ------------------
///
/// The Run Type Flags determines if the value/count is a run of the same value, or a start point in the Bit Pack/VLD data that is read
/// forward by the count. It is how the segment interleaves runs with non-sequential data.
///
/// RLE Type
/// --------
/// Value will be either a literal Data Id or a pointer to the Bit Pack Array.
///
/// -Value - 1 is the index in the Bit Pack Array.
///
/// If Value is 4 bytes the flag is the high (sign) bit of the Value. Value less than zero is a read, value greater or equal to zero is a
/// run (as the run is defined by the literal value rather than reference to the Bit Pack Array).
///
/// 8 byte values have to be able to represent the full 64-bit space so the bit flag is moved to a separate 4 byte structure at the end of
/// the entry where the low bit (or full value as it only represents 1 or 0) is the read marker.
///
/// VLD Type
/// --------
///
/// Value will always be a pointer to a VLD Value Page. VLD will always use a 4-byte value. The layout is:
///
///  - Bits 0 - 14   Page Index
///  - Bits 15 - 29  Slot Index
///  - Bit  30       Terminator/Read 0:0 disambiguator
///  - Bit  31       Read Flag
///
/// The additional Repeat Flag bit is to differentiate an address of 0:0 vs the terminator 0 value.
///
/// Terminator
/// ----------
///
/// Value = 0, Run Count = 0 is the run terminator marker.
/// </remarks>
public readonly record struct RleEntry(long Value, int Count, bool IsVariableLengthData = false, int? ReadFlag = null)
{
    public const long VariableLengthRepeatFlag = 0x40000000;

    public bool IsPureValue => ReadFlag is { } flag ? flag == 0 : Value >= 0;

    public bool HasRepeatFlag => IsVariableLengthData && (Value & VariableLengthRepeatFlag) != 0;

    public SegmentPageSlot? PageSlot => IsVariableLengthData && !IsTerminator
                                        ? new SegmentPageSlot((int)(Value & 0x7FFF), (int)((Value & 0x3FFF8000) >> 15))
                                        : null;

    public bool IsTerminator => Value == 0 && Count == 0;

    /// <summary>
    /// Index of the first bit packed value the run covers
    /// </summary>
    public int BitpackIndex => (int)(-Value - 1);
}
