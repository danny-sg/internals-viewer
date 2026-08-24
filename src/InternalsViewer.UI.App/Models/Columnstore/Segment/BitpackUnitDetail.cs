using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// One packed unit broken into the values it holds, for the bit ruler and the value list beneath it
/// </summary>
/// <remarks>
/// The members are settable rather than required because the type backs a dependency property, and the generated
/// XAML type info constructs one without arguments.
/// </remarks>
public sealed class BitpackUnitDetail
{
    public int UnitIndex { get; init; }

    public int Offset { get; init; }

    public ulong Bits { get; init; }

    public int EntrySizeBits { get; init; }

    /// <summary>
    /// Bits at the top of the unit that no value reaches, the entry size rarely dividing sixty four exactly
    /// </summary>
    public int PaddingBits { get; init; }

    public IReadOnlyList<BitpackValueDetail> Values { get; init; } = [];

    public string OffsetDescription => $"0x{Offset:X}";

    public static BitpackUnitDetail Build(SegmentBlob blob, int unitIndex, Func<long, ValueDerivation?>? valueDerivation = null,
                                          bool showDerivation = true)
    {
        var array = blob.Bitpack;

        var perUnit = array.ValuesPerUnit;

        var values = new List<BitpackValueDetail>(perUnit);

        for (var i = 0; i < perUnit; i++)
        {
            var index = (unitIndex * perUnit) + i;

            var span = array.GetSpan(index);

            values.Add(new BitpackValueDetail
            {
                Index = index,
                BitOffset = span.BitOffset - (unitIndex * BitpackArray.UnitBits),
                BitLength = span.BitLength,
                DataId = array[index],
                MinId = array.MinId,
                ValueDerivation = valueDerivation?.Invoke(array[index]),
                ShowDerivation = showDerivation
            });
        }

        return new BitpackUnitDetail
        {
            UnitIndex = unitIndex,
            Offset = blob.Header.BitpackArrayOffset + (unitIndex * BitpackArray.UnitBytes),
            Bits = ReadUnit(blob, unitIndex),
            EntrySizeBits = array.EntrySizeBits,
            PaddingBits = BitpackArray.UnitBits - (perUnit * array.EntrySizeBits),
            Values = values
        };
    }

    private static ulong ReadUnit(SegmentBlob blob, int unitIndex)
    {
        var span = blob.Bitpack.Data.Span.Slice(unitIndex * BitpackArray.UnitBytes, BitpackArray.UnitBytes);

        return System.BitConverter.ToUInt64(span);
    }
}
