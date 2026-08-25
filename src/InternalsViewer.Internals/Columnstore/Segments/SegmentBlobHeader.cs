using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// The fixed prologue of a column segment blob, from which the whole layout of the blob follows
/// </summary>
/// <remarks>
/// Every offset and size in the blob is derived from these fields alone, so reading the prologue is enough to
/// describe a segment without fetching the arrays behind it.
/// </remarks>
public sealed class SegmentBlobHeader : DataStructure
{
    public const int Size = 48;

    public const int NativeUnitSize = 8;

    private const int VariableLengthBookmarkOverlap = 2;

    [DataStructureItem(ItemType.SegmentVersion)]
    public int Version { get; set; }

    [DataStructureItem(ItemType.SegmentLobType)]
    public ColumnstoreLobType LobType { get; set; }

    [DataStructureItem(ItemType.SegmentReserved)]
    public int Reserved { get; set; }

    [DataStructureItem(ItemType.SegmentUnknown)]
    public int Unknown0C { get; set; }

    /// <summary>
    /// Layout of the value stream, which is not the RLE and bit pack pair for every encoding
    /// </summary>
    [DataStructureItem(ItemType.SegmentRleType)]
    public SegmentRleType RleType { get; set; }

    [DataStructureItem(ItemType.BookmarkCount)]
    public int BookmarkCount { get; set; }

    [DataStructureItem(ItemType.BookmarkDistance)]
    public int BookmarkDistance { get; set; }

    /// <summary>
    /// How many 8-byte native units the array occupies
    /// </summary>
    [DataStructureItem(ItemType.RleArrayCount)]
    public int RleArrayCount { get; set; }

    /// <summary>
    /// Size of the native unit
    /// </summary>
    [DataStructureItem(ItemType.RleEntrySize)]
    public short RleEntrySize { get; set; }

    [DataStructureItem(ItemType.BitpackEntrySize)]
    public short BitpackEntrySize { get; set; }

    [DataStructureItem(ItemType.BitpackUnitCount)]
    public int BitpackUnitCount { get; set; }

    /// <summary>
    /// Lowest data id in the segment, subtracted from every packed value
    /// </summary>
    [DataStructureItem(ItemType.BitpackMinId)]
    public long BitpackMinId { get; set; }

    /// <summary>
    /// Width of one RLE entry
    /// </summary>
    /// <remarks>
    /// Eight bytes carry an int32 value and an int32 count, sixteen an int64 value and an int32 count. Nothing in the
    /// header separates the two - segments that agree on every field here disagree on the width - so the parser
    /// works it out from base id and magnitude and sets it. Eight until it says otherwise.
    /// </remarks>
    public int RleEntryBytes { get; set; } = NativeUnitSize;

    /// <summary>
    /// RleArrayCount is held in eight byte native units rather than entries
    /// </summary>
    public int RleEntryCount => RleArrayCount * NativeUnitSize / RleEntryBytes;

    public bool IsVariableLengthData => RleType == SegmentRleType.VariableLengthData;

    public bool HasRleArray => RleArrayCount > 0;

    public bool HasBitpackArray => RleType == SegmentRleType.BitPack && BitpackUnitCount > 0;

    /// <summary>
    /// Values packed into each unit, the remainder of sixty four being left unused
    /// </summary>
    public int BitpackValuesPerUnit => BitpackEntrySize > 0 ? BitpackArray.UnitBits / BitpackEntrySize : 0;

    public int BitpackValueCount => BitpackValuesPerUnit * BitpackUnitCount;

    /// <summary>
    /// Bytes before the bookmark array, the store by value layout carrying two more than the run length one
    /// </summary>
    public int PrologueSize => IsVariableLengthData ? Size + 2 : Size;

    public int BookmarkArrayOffset => PrologueSize;

    public int BookmarkEntryCount => IsVariableLengthData
        ? Math.Max(0, BookmarkCount - VariableLengthBookmarkOverlap)
        : BookmarkCount;

    public int RleArrayOffset => BookmarkArrayOffset + (BookmarkEntryCount * NativeUnitSize);

    public int VariableLengthDataOffset => RleArrayOffset + (RleArrayCount * NativeUnitSize);

    public int BitpackArrayOffset => RleArrayOffset + (RleArrayCount * NativeUnitSize);

    /// <summary>
    /// Size the header fields imply, which must equal on_disk_size and the blob length
    /// </summary>
    public int ExpectedSize => Size + (NativeUnitSize * (RleArrayCount + BookmarkCount + BitpackUnitCount));

    /// <summary>
    /// Records the fields against the blob, the prologue being the opening bytes of it
    /// </summary>
    public void Mark()
    {
        MarkProperty(nameof(Version), 0x00, 4);
        MarkProperty(nameof(LobType), 0x04, 4);
        MarkProperty(nameof(Reserved), 0x08, 4);
        MarkProperty(nameof(Unknown0C), 0x0C, 4);
        MarkProperty(nameof(RleType), 0x10, 4);
        MarkProperty(nameof(BookmarkCount), 0x14, 4);
        MarkProperty(nameof(BookmarkDistance), 0x18, 4);
        MarkProperty(nameof(RleArrayCount), 0x1C, 4);
        MarkProperty(nameof(RleEntrySize), 0x20, 2);
        MarkProperty(nameof(BitpackEntrySize), 0x22, 2);
        MarkProperty(nameof(BitpackUnitCount), 0x24, 4);
        MarkProperty(nameof(BitpackMinId), 0x28, 8);
    }
}
