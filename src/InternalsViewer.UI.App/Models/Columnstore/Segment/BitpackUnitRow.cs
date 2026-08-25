using System;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// One packed unit as the table lists it, worked out only when a row asks for it
/// </summary>
public sealed class BitpackUnitRow(SegmentBlob blob, int unit) : IEquatable<BitpackUnitRow>
{
    public int Unit { get; } = unit;

    public int Offset => blob.Header.BitpackArrayOffset + (Unit * BitpackArray.UnitBytes);

    public string OffsetDescription => $"0x{Offset:X}";

    /// <summary>
    /// Working from the unit to where it starts, the array being addressed in eight byte units from its own offset
    /// </summary>
    public ValueDerivation Derivation => field ??= new()
    {
        Steps =
        [
            new DerivationStep { Name = "Unit", Value = $"{Unit}" },
            new DerivationStep { Operator = "*", Name = "Unit Size", Value = $"{BitpackArray.UnitBytes}" },
            new DerivationStep { Operator = "+", Name = "Array Offset", Value = $"{blob.Header.BitpackArrayOffset}" }
        ],
        Result = OffsetDescription,
        Target = new SegmentNavigationTarget(SegmentRegion.BitpackArray, Offset)
    };

    public ulong Bits => BitConverter.ToUInt64(blob.Bitpack.Data.Span.Slice(Unit * BitpackArray.UnitBytes,
                                                                            BitpackArray.UnitBytes));

    public string BitsDescription => $"0x{Bits:X16}";

    public int FirstValue => Unit * blob.Bitpack.ValuesPerUnit;

    public string ValueRange => blob.Bitpack.ValuesPerUnit switch
    {
        <= 0 => string.Empty,
        1 => $"{FirstValue}",
        _ => $"{FirstValue} - {FirstValue + blob.Bitpack.ValuesPerUnit - 1}"
    };

    public bool Equals(BitpackUnitRow? other) => other is not null && other.Unit == Unit;

    public override bool Equals(object? obj) => Equals(obj as BitpackUnitRow);

    public override int GetHashCode() => Unit;
}
