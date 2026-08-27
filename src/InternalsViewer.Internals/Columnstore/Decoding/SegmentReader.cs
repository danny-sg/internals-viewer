using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Columnstore.Decoding;

/// <summary>
/// Reads decoded values out of a parsed column segment
/// </summary>
public sealed class SegmentReader(ColumnSegment segment,
                                 SegmentBlob blob,
                                 DictionaryBlob? dictionary,
                                 DictionaryBlob? overflow = null)
{
    public ColumnSegment Segment { get; } = segment;

    public SegmentBlob Blob { get; } = blob;

    public SegmentDataIdStream DataIds { get; } = new(blob);

    public IEnumerable<long> DictionaryDataIds
    {
        get
        {
            if (Dictionary is { } primary)
            {
                for (var id = primary.FirstId; id <= primary.LastId; id++)
                {
                    yield return id;
                }
            }

            if (Overflow is not { } second)
            {
                yield break;
            }

            var offset = Dictionary?.LastId ?? 0;

            for (var id = second.FirstId; id <= second.LastId; id++)
            {
                yield return offset + id;
            }
        }
    }

    public int RowCount => DataIds.RowCount;

    private DictionaryBlob? Dictionary { get; } = dictionary;

    private DictionaryBlob? Overflow { get; } = overflow;

    private SegmentValueDecoder Decoder { get; } = new(segment, dictionary, overflow);

    public object? GetRawValue(int rowOrdinal)
    {
        if (Blob.VariableLengthData is not { } store)
        {
            return Decoder.Decode(DataIds.GetRowDataId(rowOrdinal));
        }

        var (_, valueOrdinal) = DataIds.FindValue(rowOrdinal);

        if (valueOrdinal < 0)
        {
            return null;
        }

        if (!store.IsWide)
        {
            return Decoder.Decode(store.GetValue(valueOrdinal));
        }

        return store.GetValueBytes(valueOrdinal) is { IsEmpty: false } bytes ? bytes.ToArray() : null;
    }

    public object? GetValue(int rowOrdinal)
        => ColumnstoreValueConverter.Convert(GetRawValue(rowOrdinal), Segment.Column?.Structure);

    /// <summary>
    /// Gets the value a data id names, for a caller holding the id rather than a row ordinal
    /// </summary>
    /// <remarks>
    /// Wide values are located by value ordinal rather than by data id, so a segment holding them cannot be read
    /// this way.
    /// </remarks>
    public object? GetValueForDataId(long dataId)
        => ColumnstoreValueConverter.Convert(Decoder.Decode(dataId), Segment.Column?.Structure);

    public IEnumerable<object?> ReadAll()
        => DataIds.ReadAll()
                  .Select(Decoder.Decode)
                  .Select(v => ColumnstoreValueConverter.Convert(v, Segment.Column?.Structure));
}
