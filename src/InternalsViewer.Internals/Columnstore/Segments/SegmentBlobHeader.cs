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

    public const int EntrySize = 8;

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
    [DataStructureItem(ItemType.SegmentStructureType)]
    public SegmentStructureType StructureType { get; set; }

    [DataStructureItem(ItemType.BookmarkCount)]
    public int BookmarkCount { get; set; }

    [DataStructureItem(ItemType.BookmarkDistance)]
    public int BookmarkDistance { get; set; }

    [DataStructureItem(ItemType.RleArrayCount)]
    public int RleArrayCount { get; set; }

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
    /// Width of one RLE entry, which doubles when a data id no longer fits in four bytes
    /// </summary>
    public int RleEntryBytes => BitpackEntrySize > 32 ? EntrySize * 2 : EntrySize;

    /// <summary>
    /// RleArrayCount is held in eight byte units rather than entries
    /// </summary>
    public int RleEntryCount => RleArrayCount * EntrySize / RleEntryBytes;

    public bool IsStoreByValue => StructureType == SegmentStructureType.StoreByValue;

    public bool HasRleArray => StructureType == SegmentStructureType.RunLength && RleArrayCount > 0;

    public bool HasBitpackArray => StructureType == SegmentStructureType.RunLength && BitpackUnitCount > 0;

    /// <summary>
    /// Values packed into each unit, the remainder of sixty four being left unused
    /// </summary>
    public int BitpackValuesPerUnit => BitpackEntrySize > 0 ? BitpackArray.UnitBits / BitpackEntrySize : 0;

    public int BitpackValueCount => BitpackValuesPerUnit * BitpackUnitCount;

    /// <summary>
    /// Bytes before the bookmark array, the store by value layout carrying two more than the run length one
    /// </summary>
    public int PrologueSize => IsStoreByValue ? Size + 2 : Size;

    public int BookmarkArrayOffset => PrologueSize;

    /// <summary>
    /// Units of the RLE array a store by value segment writes out, the prologue already covering the first two
    /// </summary>
    /// <remarks>
    /// A nullable column raises RleArrayCount above the two a plain one carries and writes the extra units between
    /// the bookmarks and the store, so the store moves down by them. Measured on uniqueidentifier, binary and
    /// datetimeoffset columns at counts of 2, 3 and 4, whose stores sat at +0, +8 and +16.
    /// </remarks>
    public int TrailingRleUnits => IsStoreByValue ? Math.Max(0, RleArrayCount - 2) : 0;

    public int VariableLengthDataOffset
        => BookmarkArrayOffset + (BookmarkCount * EntrySize) + (TrailingRleUnits * EntrySize);

    public int RleArrayOffset => BookmarkArrayOffset + (BookmarkCount * EntrySize);

    public int BitpackArrayOffset => RleArrayOffset + (RleArrayCount * EntrySize);

    /// <summary>
    /// Size the header fields imply, which must equal on_disk_size and the blob length
    /// </summary>
    public int ExpectedSize => Size + (EntrySize * (RleArrayCount + BookmarkCount + BitpackUnitCount));

    /// <summary>
    /// Records the fields against the blob, the prologue being the opening bytes of it
    /// </summary>
    public void Mark()
    {
        MarkProperty(nameof(Version), 0x00, 4);
        MarkProperty(nameof(LobType), 0x04, 4);
        MarkProperty(nameof(Reserved), 0x08, 4);
        MarkProperty(nameof(Unknown0C), 0x0C, 4);
        MarkProperty(nameof(StructureType), 0x10, 4);
        MarkProperty(nameof(BookmarkCount), 0x14, 4);
        MarkProperty(nameof(BookmarkDistance), 0x18, 4);
        MarkProperty(nameof(RleArrayCount), 0x1C, 4);
        MarkProperty(nameof(RleEntrySize), 0x20, 2);
        MarkProperty(nameof(BitpackEntrySize), 0x22, 2);
        MarkProperty(nameof(BitpackUnitCount), 0x24, 4);
        MarkProperty(nameof(BitpackMinId), 0x28, 8);
    }
}
