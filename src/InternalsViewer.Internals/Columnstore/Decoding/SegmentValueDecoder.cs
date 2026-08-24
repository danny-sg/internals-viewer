using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Internals.Columnstore.Decoding;

/// <summary>
/// Decodes Data Id to Value for a Segment
/// </summary>
public sealed class SegmentValueDecoder(ColumnSegment segment,
                                        DictionaryBlob? dictionary,
                                        DictionaryBlob? overflow = null)
{
    private ColumnSegment Segment { get; } = segment;

    private DictionaryBlob? Dictionary { get; } = dictionary;

    private DictionaryBlob? Overflow { get; } = overflow;

    public object? Decode(long dataId)
    {
        if (Segment.HasNulls && Segment.NullValue == dataId)
        {
            return null;
        }

        var (source, id) = Resolve(dataId);

        return source switch
        {
            StringDictionary strings 
                => strings.GetValueBytes(id),
            NumericDictionary numbers 
                => numbers.GetValue(id),
            _ => DecodeValueBased(id)
        };
    }

    /// <summary>
    /// Gets the Dictionary/relative Data Id for a Data Id
    /// </summary>
    public (DictionaryBlob? Dictionary, long DataId) Resolve(long dataId)
    {
        if (Dictionary != null && Overflow is { } second && dataId > Dictionary.LastId)
        {
            return (second, dataId - Dictionary.LastId);
        }

        return (Dictionary ?? Overflow, dataId);
    }

    private object DecodeValueBased(long dataId)
    {
        var value = dataId + Segment.BaseId;

        if (Segment.Magnitude > 0 && Math.Abs(Segment.Magnitude - 1) > double.Epsilon)
        {
            return value * (decimal)Segment.Magnitude;
        }

        return value;
    }
}
