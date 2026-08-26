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
    [DataStructureItem(ItemType.RleArrayEntrySize)]
    public short RleArrayEntrySize { get; set; }

    [DataStructureItem(ItemType.BitpackEntrySize)]
    public short BitpackEntrySize { get; set; }

    [DataStructureItem(ItemType.BitpackUnitCount)]
    public int BitpackUnitCount { get; set; }

    /// <summary>
    /// Lowest data id in the segment, subtracted from every packed value
    /// </summary>
    [DataStructureItem(ItemType.BitpackMinId)]
    public long BitpackMinId { get; set; }

    public int RleValueSize { get; set; } = sizeof(int);

    /// <summary>
    /// Width of one RLE entry in the RLE array
    /// </summary>
    /// <remarks>
    /// Entry Size = Value Size (4 bytes or 8 bytes) + Run Count (4 bytes)
    ///              + If Value Size = 8 bytes -> + Read Flag (4 bytes)
    /// </remarks>
    public int RleEntrySize => RleValueSize + sizeof(int) + (RleValueSize == sizeof(long) ? sizeof(int) : 0);

    /// <summary>
    /// RleArrayCount is held in eight byte native units rather than entries
    /// </summary>
    public int RleEntryCount => RleArrayCount * NativeUnitSize / RleEntrySize;

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

    public int BookmarkEntryCount
        => IsVariableLengthData
            ? Math.Max(0, BookmarkCount - VariableLengthBookmarkOverlap)
            : BookmarkCount;

    /// <summary>
    /// Offset of the RLE array, which is after the bookmark array
    /// </summary>
    public int RleArrayOffset
        => BookmarkArrayOffset + (BookmarkEntryCount * NativeUnitSize);

    /// <remarks>
    /// RLE size is slightly confusing and the raw header RLE Entry Count is in terms of 8-byte native units, not the actual entry size,
    /// but for the raw size RLE Array Count (in native units) * Native Unit Size is the correct size of the RLE array in bytes
    /// </remarks>
    public int RleArraySize => RleArrayCount * NativeUnitSize;

    /// <summary>
    /// Offset of the variable length data, which is after the RLE array in VLD sub-types
    /// </summary>
    public int VariableLengthDataOffset => RleArrayOffset + RleArraySize;

    /// <summary>
    /// Offset of the bit pack array, which is after the RLE array in RLE sub-types
    /// </summary>
    public int BitpackArrayOffset => RleArrayOffset + RleArraySize;

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
        MarkProperty(nameof(RleArrayEntrySize), 0x20, 2);
        MarkProperty(nameof(BitpackEntrySize), 0x22, 2);
        MarkProperty(nameof(BitpackUnitCount), 0x24, 4);
        MarkProperty(nameof(BitpackMinId), 0x28, 8);
    }
}
