using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Parsed column segment blob
/// </summary>
public sealed class SegmentBlob : DataStructure
{
    public const int HeaderSize = SegmentBlobHeader.Size;

    public const int EntrySize = SegmentBlobHeader.EntrySize;

    public ReadOnlyMemory<byte> Data { get; set; }

    /// <summary>
    /// The prologue the rest of the blob is laid out from, which a header only read produces on its own
    /// </summary>
    public SegmentBlobHeader Header { get; set; } = new();

    [DataStructureItem(ItemType.SegmentVersion)]
    public int Version { get => Header.Version; set => Header.Version = value; }

    [DataStructureItem(ItemType.SegmentLobType)]
    public ColumnstoreLobType LobType { get => Header.LobType; set => Header.LobType = value; }

    [DataStructureItem(ItemType.SegmentReserved)]
    public int Reserved { get => Header.Reserved; set => Header.Reserved = value; }

    [DataStructureItem(ItemType.SegmentUnknown)]
    public int Unknown0C { get => Header.Unknown0C; set => Header.Unknown0C = value; }

    [DataStructureItem(ItemType.SegmentStructureType)]
    public SegmentStructureType StructureType { get => Header.StructureType; set => Header.StructureType = value; }

    [DataStructureItem(ItemType.BookmarkCount)]
    public int BookmarkCount { get => Header.BookmarkCount; set => Header.BookmarkCount = value; }

    [DataStructureItem(ItemType.BookmarkDistance)]
    public int BookmarkDistance { get => Header.BookmarkDistance; set => Header.BookmarkDistance = value; }

    [DataStructureItem(ItemType.RleArrayCount)]
    public int RleArrayCount { get => Header.RleArrayCount; set => Header.RleArrayCount = value; }

    [DataStructureItem(ItemType.RleEntrySize)]
    public short RleEntrySize { get => Header.RleEntrySize; set => Header.RleEntrySize = value; }

    [DataStructureItem(ItemType.BitpackEntrySize)]
    public short BitpackEntrySize { get => Header.BitpackEntrySize; set => Header.BitpackEntrySize = value; }

    [DataStructureItem(ItemType.BitpackUnitCount)]
    public int BitpackUnitCount { get => Header.BitpackUnitCount; set => Header.BitpackUnitCount = value; }

    [DataStructureItem(ItemType.BitpackMinId)]
    public long BitpackMinId { get => Header.BitpackMinId; set => Header.BitpackMinId = value; }

    public SegmentBookmark[] Bookmarks { get; set; } = [];

    public RleEntry[] RleEntries { get; set; } = [];

    public BitpackArray Bitpack { get; set; }

    public int RleEntryBytes => Header.RleEntryBytes;

    public int RleEntryCount => Header.RleEntryCount;

    public SegmentValueStore? ValueStore { get; set; }

    public bool IsStoreByValue => Header.IsStoreByValue;

    public int PrologueSize => Header.PrologueSize;

    public int BookmarkArrayOffset => Header.BookmarkArrayOffset;

    public int ValueStoreOffset => Header.ValueStoreOffset;

    public int RleArrayOffset => Header.RleArrayOffset;

    public int BitpackArrayOffset => Header.BitpackArrayOffset;

    public int ExpectedSize => Header.ExpectedSize;

    /// <summary>
    /// Rows the RLE runs cover, excluding the terminator
    /// </summary>
    public int RowCount => ValueStore?.ValueCount ?? RleEntries.Sum(e => e.Count);

    public int BitpackRowCount => RleEntries.Where(e => e.IsBitpacked).Sum(e => e.Count);

    public int LiteralRunCount => RleEntries.Count(e => e is { IsBitpacked: false, Count: > 0 });
}
