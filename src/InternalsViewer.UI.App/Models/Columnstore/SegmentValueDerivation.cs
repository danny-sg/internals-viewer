using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// The working from a data id to the value it stands for, which differs by how the column was encoded
/// </summary>
public static class SegmentValueDerivation
{
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

        return HasDictionary(segment)
            ? new ValueDerivation
            {
                Steps = [new DerivationStep { Name = "Dictionary Entry", Value = $"{dataId}" }],
                Result = result
            }
            : new ValueDerivation { Steps = [.. ValueBasedSteps(segment, dataId)], Result = result };
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

    private static bool HasDictionary(ColumnSegment segment)
        => (segment.SecondaryDictionaryId >= 0 ? segment.LocalDictionary : segment.Column?.GlobalDictionary) is not null;

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
