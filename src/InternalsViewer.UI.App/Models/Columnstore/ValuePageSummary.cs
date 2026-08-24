using System;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One page of a store by value segment as the viewer presents it
/// </summary>
public sealed class ValuePageSummary
{
    public required int Index { get; init; }

    public required SegmentValuePage Page { get; init; }

    public required int Offset { get; init; }

    public required int Size { get; init; }

    public string SubLobTypeDescription => Page.SubLobType.ToString().SplitCamelCase();

    public int ValueCount => Page.ValueCount;

    public int ValueSize => Page.ValueSize;

    public bool IsCompressed => Page.IsCompressed;

    public string OffsetDescription => $"0x{Offset:X}";

    /// <summary>
    /// What the page costs against what it holds, the values being Xpress Huffman compressed on the page
    /// </summary>
    public string CompressionDescription => Page.ExpandedSize == 0
        ? string.Empty
        : $"{Page.Size} of {Page.ExpandedSize}";
}

/// <summary>
/// One value of a store by value page, worked out only when something asks for it
/// </summary>
public sealed class ValueDetail(SegmentValuePage page, int index) : IEquatable<ValueDetail>
{
    public int Index { get; } = index;

    /// <summary>
    /// Whether the value is too wide to be an integer, in which case it is the bytes rather than a number
    /// </summary>
    public bool IsWide => page.IsWide;

    public long Raw => IsWide ? 0 : page.GetRawValue(Index);

    /// <summary>
    /// The value the low reserved bit has been taken off, which is what the segment reads back
    /// </summary>
    public long Value => Raw >> SegmentVariableLengthData.ReservedBits;

    /// <summary>
    /// Where the offset array puts the value, which only a variable width page has to say
    /// </summary>
    public string OffsetDescription
        => page.IsVariableWidth ? $"0x{page.GetStoredOffset(Index):X4}" : string.Empty;

    /// <summary>
    /// What the page holds for the value, a wide one having no integer form to show
    /// </summary>
    public string StoredDescription
        => page.IsNull(Index)
            ? "[Null]"
            : IsWide ? $"0x{Convert.ToHexString(page.GetValueBytes(Index).Span)}" : $"{Raw}";

    /// <summary>
    /// Working from the stored integer to the value, the low bit being reserved rather than part of it
    /// </summary>
    public ValueDerivation Derivation => IsWide
        ? new ValueDerivation { Steps = [], Result = StoredDescription }
        : new ValueDerivation
        {
            Steps =
            [
                new DerivationStep { Name = "Stored", Value = $"{Raw}" },
                new DerivationStep { Operator = ">>", Name = "Reserved Bits", Value = $"{SegmentVariableLengthData.ReservedBits}" }
            ],
            Result = $"{Value}"
        };

    public bool Equals(ValueDetail? other) => other is not null && other.Index == Index;

    public override bool Equals(object? obj) => Equals(obj as ValueDetail);

    public override int GetHashCode() => Index;
}
