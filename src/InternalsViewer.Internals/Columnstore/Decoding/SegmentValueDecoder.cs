using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Internals.Columnstore.Decoding;

/// <summary>
/// Maps a segment data id to the value it represents
/// </summary>
public sealed class SegmentValueDecoder(ColumnSegment segment, DictionaryBlob? dictionary)
{
    private ColumnSegment Segment { get; } = segment;

    private DictionaryBlob? Dictionary { get; } = dictionary;

    public object? Decode(long dataId)
    {
        if (Segment.HasNulls && Segment.NullValue == dataId)
        {
            return null;
        }

        return Dictionary switch
        {
            StringDictionary strings => strings.GetValue(dataId),
            NumericDictionary numbers => numbers.GetValue(dataId),
            _ => DecodeValueBased(dataId)
        };
    }

    private object DecodeValueBased(long dataId)
    {
        var value = dataId + Segment.BaseId;

        if (Segment.Magnitude > 0 && Math.Abs(Segment.Magnitude - 1) > double.Epsilon)
        {
            return value / (decimal)Segment.Magnitude;
        }

        return value;
    }
}
