using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Parsed Column Segment
/// </summary>
public sealed class SegmentBlob : DataStructure
{
    public const int HeaderSize = SegmentBlobHeader.Size;

    public const int EntrySize = SegmentBlobHeader.EntrySize;

    public ColumnSegment? Segment { get; set; }

    public ReadOnlyMemory<byte> Data { get; set; }

    public SegmentBlobHeader Header { get; set; } = new();

    public SegmentBookmark[] Bookmarks { get; set; } = [];

    public RleEntry[] RleEntries { get; set; } = [];

    public BitpackArray Bitpack { get; set; }

    public SegmentVariableLengthData? VariableLengthData { get; set; }

    public int RowCount => RleEntries.Length > 0 ? RleEntries.Sum(e => e.Count) : VariableLengthData?.ValueCount ?? 0;

    public int BitpackRowCount => RleEntries.Where(e => !e.IsValue).Sum(e => e.Count);

    public int LiteralRunCount => RleEntries.Count(e => e is { IsValue: true, Count: > 0 });
}
