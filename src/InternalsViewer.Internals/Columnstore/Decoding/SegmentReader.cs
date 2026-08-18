using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Columnstore.Decoding;

/// <summary>
/// Reads decoded values out of a parsed column segment
/// </summary>
public sealed class SegmentReader(ColumnSegment segment, SegmentBlob blob, DictionaryBlob? dictionary)
{
    public ColumnSegment Segment { get; } = segment;

    public SegmentBlob Blob { get; } = blob;

    public SegmentDataIdStream DataIds { get; } = new(blob);

    public int RowCount => DataIds.RowCount;

    private SegmentValueDecoder Decoder { get; } = new(segment, dictionary);

    public object? GetValue(int rowOrdinal) => Decoder.Decode(DataIds.GetDataId(rowOrdinal));

    public IEnumerable<object?> ReadAll() => DataIds.ReadAll().Select(Decoder.Decode);
}
