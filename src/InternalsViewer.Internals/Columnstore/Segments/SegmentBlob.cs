using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Parsed column segment blob
/// </summary>
public sealed class SegmentBlob : DataStructure
{
    public const int HeaderSize = 48;

    public const int EntrySize = 8;

    public ReadOnlyMemory<byte> Data { get; set; }

    [DataStructureItem(ItemType.SegmentVersion)]
    public int Version { get; set; }

    [DataStructureItem(ItemType.SegmentUnknown)]
    public int Unknown04 { get; set; }

    [DataStructureItem(ItemType.SegmentUnknown)]
    public int Unknown08 { get; set; }

    [DataStructureItem(ItemType.SegmentUnknown)]
    public int Unknown0C { get; set; }

    [DataStructureItem(ItemType.SegmentLobType)]
    public ColumnstoreLobType LobType { get; set; }

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

    [DataStructureItem(ItemType.BitpackMinId)]
    public int BitpackMinId { get; set; }

    [DataStructureItem(ItemType.SegmentUnknown)]
    public int Unknown2C { get; set; }

    public SegmentBookmark[] Bookmarks { get; set; } = [];

    public RleEntry[] RleEntries { get; set; } = [];

    public BitpackArray Bitpack { get; set; }

    public int BookmarkArrayOffset => HeaderSize;

    public int RleArrayOffset => BookmarkArrayOffset + (BookmarkCount * EntrySize);

    public int BitpackArrayOffset => RleArrayOffset + (RleArrayCount * EntrySize);

    /// <summary>
    /// Size the header fields imply, which must equal on_disk_size and the blob length
    /// </summary>
    public int ExpectedSize => HeaderSize + (EntrySize * (RleArrayCount + BookmarkCount + BitpackUnitCount));

    /// <summary>
    /// Rows the RLE runs cover, excluding the terminator
    /// </summary>
    public int RowCount => RleEntries.Sum(e => e.Count);

    public int BitpackRowCount => RleEntries.Where(e => e.IsBitpacked).Sum(e => e.Count);

    public int LiteralRunCount => RleEntries.Count(e => !e.IsBitpacked && e.Count > 0);
}
