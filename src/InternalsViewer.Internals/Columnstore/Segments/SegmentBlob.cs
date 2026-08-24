using InternalsViewer.Internals.Annotations;

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

    public SegmentBookmark[] Bookmarks { get; set; } = [];

    public RleEntry[] RleEntries { get; set; } = [];

    public BitpackArray Bitpack { get; set; }

    public SegmentVariableLengthData? VariableLengthData { get; set; }

    /// <summary>
    /// Rows the RLE runs cover, excluding the terminator
    /// </summary>
    public int RowCount => VariableLengthData?.ValueCount ?? RleEntries.Sum(e => e.Count);

    public int BitpackRowCount => RleEntries.Where(e => e.IsBitpacked).Sum(e => e.Count);

    public int LiteralRunCount => RleEntries.Count(e => e is { IsBitpacked: false, Count: > 0 });
}
