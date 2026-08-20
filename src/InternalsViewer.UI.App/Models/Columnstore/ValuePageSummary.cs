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

    public string SubLobTypeDescription => Page.SubLobType.ToString().SplitCamelCase();

    public int ValueCount => Page.ValueCount;

    public int ValueSize => Page.ValueSize;

    public int Offset => Page.Offset;

    public int Size => Page.Size;

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

    public long Raw => page.GetRawValue(Index);

    /// <summary>
    /// The value the low reserved bit has been taken off, which is what the segment reads back
    /// </summary>
    public long Value => Raw >> SegmentValueStore.ReservedBits;

    /// <summary>
    /// Working from the stored integer to the value, the low bit being reserved rather than part of it
    /// </summary>
    public ValueDerivation Derivation => new()
    {
        Steps =
        [
            new DerivationStep { Name = "Stored", Value = $"{Raw}" },
            new DerivationStep { Operator = ">>", Name = "Reserved Bits", Value = $"{SegmentValueStore.ReservedBits}" }
        ],
        Result = $"{Value}"
    };

    public bool Equals(ValueDetail? other) => other is not null && other.Index == Index;

    public override bool Equals(object? obj) => Equals(obj as ValueDetail);

    public override int GetHashCode() => Index;
}
