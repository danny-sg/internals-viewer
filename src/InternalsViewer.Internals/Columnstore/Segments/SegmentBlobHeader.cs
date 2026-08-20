using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// The fixed prologue of a column segment blob, from which the whole layout of the blob follows
/// </summary>
/// <remarks>
/// Every offset and size in the blob is derived from these fields alone, so reading the prologue is enough to
/// describe a segment without fetching the arrays behind it.
/// </remarks>
public sealed class SegmentBlobHeader
{
    public const int Size = 48;

    public const int EntrySize = 8;

    public int Version { get; set; }

    public ColumnstoreLobType LobType { get; set; }

    public int Reserved { get; set; }

    public int Unknown0C { get; set; }

    /// <summary>
    /// Layout of the value stream, which is not the RLE and bit pack pair for every encoding
    /// </summary>
    public SegmentStructureType StructureType { get; set; }

    public int BookmarkCount { get; set; }

    public int BookmarkDistance { get; set; }

    public int RleArrayCount { get; set; }

    public short RleEntrySize { get; set; }

    public short BitpackEntrySize { get; set; }

    public int BitpackUnitCount { get; set; }

    /// <summary>
    /// Lowest data id in the segment, subtracted from every packed value
    /// </summary>
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

    public int ValueStoreOffset => BookmarkArrayOffset + (BookmarkCount * EntrySize);

    public int RleArrayOffset => BookmarkArrayOffset + (BookmarkCount * EntrySize);

    public int BitpackArrayOffset => RleArrayOffset + (RleArrayCount * EntrySize);

    /// <summary>
    /// Size the header fields imply, which must equal on_disk_size and the blob length
    /// </summary>
    public int ExpectedSize => Size + (EntrySize * (RleArrayCount + BookmarkCount + BitpackUnitCount));
}
