using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// The working from a data id to the value it stands for, which differs by how the column was encoded
/// </summary>
public static class SegmentValueDerivation
{
    /// <summary>
    /// The working for a value too wide to be a data id, which is the stored bytes read against the column
    /// </summary>
    public static ValueDerivation BuildWide(ColumnSegment segment, SegmentVariableLengthData store, int ordinal)
    {
        var bytes = store.GetValueBytes(ordinal);

        if (store.IsNull(ordinal))
        {
            return new ValueDerivation { Steps = [], Result = "[Null]" };
        }

        return new ValueDerivation
        {
            Steps = [new DerivationStep { Name = "Stored", Value = $"0x{Convert.ToHexString(bytes.Span)}" }],
            Result = $"{ColumnstoreValueConverter.Convert(bytes.ToArray(), segment.Column?.Structure)}"
        };
    }

    public static ValueDerivation Build(ColumnSegment segment, SegmentValueDecoder decoder, long dataId)
    {
        var result = Describe(decoder.Decode(dataId), segment);

        if (segment.HasNulls && segment.NullValue == dataId)
        {
            return new ValueDerivation
            {
                Steps = [new DerivationStep { Name = "Null Value", Value = $"{dataId}" }],
                Result = result
            };
        }

        return GetDictionary(segment) is { } dictionary
            ? new ValueDerivation { Steps = [.. DictionarySteps(dictionary, dataId)], Result = result }
            : new ValueDerivation { Steps = [.. ValueBasedSteps(segment, dataId)], Result = result };
    }

    /// <summary>
    /// A dictionary column looks the value up by where the data id sits from the dictionary's first id
    /// </summary>
    private static IEnumerable<DerivationStep> DictionarySteps(SegmentDictionary dictionary, long dataId)
    {
        yield return new DerivationStep { Name = "Data Id", Value = $"{dataId}" };

        yield return new DerivationStep
        {
            Operator = "-",
            Name = "First Id",
            Value = $"{dictionary.LastId - dictionary.EntryCount + 1}"
        };
    }

    /// <summary>
    /// A value based column stores an offset from a base, optionally scaled back up on the way out
    /// </summary>
    private static IEnumerable<DerivationStep> ValueBasedSteps(ColumnSegment segment, long dataId)
    {
        yield return new DerivationStep { Name = "Data Id", Value = $"{dataId}" };

        yield return new DerivationStep { Operator = "+", Name = "Base Id", Value = $"{segment.BaseId}" };

        if (segment.Magnitude > 0 && Math.Abs(segment.Magnitude - 1) > double.Epsilon)
        {
            yield return new DerivationStep { Operator = "x", Name = "Magnitude", Value = $"{segment.Magnitude}" };
        }
    }

    private static SegmentDictionary? GetDictionary(ColumnSegment segment)
        => segment.SecondaryDictionaryId >= 0 ? segment.LocalDictionary : segment.Column?.GlobalDictionary;

    private static string Describe(object? value, ColumnSegment segment)
    {
        var converted = ColumnstoreValueConverter.Convert(value, segment.Column?.Structure);

        return converted switch
        {
            null => "NULL",
            byte[] bytes => $"0x{System.Convert.ToHexString(bytes)}",
            _ => converted.ToString() ?? string.Empty
        };
    }
}
