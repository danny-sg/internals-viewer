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
    /// <summary>
    /// What the row holds before the column's type is applied
    /// </summary>
    /// <remarks>
    /// A value too wide for an integer never becomes a data id at all, so it comes back as the bytes it was
    /// stored as and the converter reads them against the column the same way a deep data field is read.
    /// </remarks>
    public object? GetRawValue(int rowOrdinal)
        => Blob.VariableLengthData is { IsWide: true } store
            ? store.GetValueBytes(rowOrdinal) is { IsEmpty: false } bytes ? bytes.ToArray() : null
            : Decoder.Decode(DataIds.GetDataId(rowOrdinal));

    public object? GetValue(int rowOrdinal)
        => ColumnstoreValueConverter.Convert(GetRawValue(rowOrdinal), Segment.Column?.Structure);

    public IEnumerable<object?> ReadAll()
        => DataIds.ReadAll()
                  .Select(Decoder.Decode)
                  .Select(v => ColumnstoreValueConverter.Convert(v, Segment.Column?.Structure));
}
