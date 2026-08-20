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

    /// <summary>
    /// Value in the segment domain, being the storage integer or the raw dictionary entry
    /// </summary>
    public object? GetRawValue(int rowOrdinal) => Decoder.Decode(DataIds.GetDataId(rowOrdinal));

    public object? GetValue(int rowOrdinal)
        => ColumnstoreValueConverter.Convert(GetRawValue(rowOrdinal), Segment.Column?.Structure);

    public IEnumerable<object?> ReadAll()
        => DataIds.ReadAll()
                  .Select(Decoder.Decode)
                  .Select(v => ColumnstoreValueConverter.Convert(v, Segment.Column?.Structure));
}
