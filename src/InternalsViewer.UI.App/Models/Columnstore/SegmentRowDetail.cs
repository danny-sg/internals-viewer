using System;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// What a row of the segment resolves to, worked out only when something asks for it
/// </summary>
/// <remarks>
/// A segment runs to a million rows, so a row is built as the grid realises it and nothing is held onto. Everything
/// here is behind a lazy field because the grid reads the same row several times over as it draws its cells, and
/// seeking a data id walks the bookmark and RLE arrays each time.
/// </remarks>
public sealed class SegmentRowDetail(SegmentRowContext context, int ordinal) : IEquatable<SegmentRowDetail>
{
    private SegmentDataIdSource? _source;

    private ValueDerivation? _dataIdDerivation;

    private ValueDerivation? _valueDerivation;

    public int Ordinal { get; } = ordinal;

    private SegmentDataIdSource Source => _source ??= context.DataIds.GetSource(Ordinal);

    public long DataId => Source.DataId;

    public string OriginDescription => Source.Origin switch
    {
        SegmentValueOrigin.BitPack => "Bit Pack",
        SegmentValueOrigin.RleRun => "RLE Run",
        SegmentValueOrigin.ValueStore => "Value Store",
        _ => string.Empty
    };

    /// <summary>
    /// The bits as they were stored, which only a bit packed row holds separately from its data id
    /// </summary>
    public string PackedDescription
        => Source.Origin == SegmentValueOrigin.BitPack ? $"{DataId - context.MinId}" : string.Empty;

    public ValueDerivation DataIdDerivation => _dataIdDerivation ??= BuildDataIdDerivation();

    public ValueDerivation? ValueDerivation => _valueDerivation ??= context.DeriveValue(DataId);

    public bool ShowDerivation => context.ShowDerivation;

    /// <summary>
    /// A row stands for its ordinal, the grid seeing a different instance each time it realises the same row
    /// </summary>
    public bool Equals(SegmentRowDetail? other) => other is not null && other.Ordinal == Ordinal;

    public override bool Equals(object? obj) => Equals(obj as SegmentRowDetail);

    public override int GetHashCode() => Ordinal;

    private ValueDerivation BuildDataIdDerivation()
    {
        var source = Source;

        var result = $"{source.DataId}";

        return source.Origin switch
        {
            SegmentValueOrigin.BitPack => new ValueDerivation
            {
                Steps =
                [
                    new DerivationStep
                    {
                        Name = "Packed Value",
                        Value = $"{source.DataId - context.MinId}",
                        Target = context.GetBitpackUnitTarget(source.BitpackIndex)
                    },
                    new DerivationStep { Operator = "+", Name = "Min Id", Value = $"{context.MinId}" }
                ],
                Result = result
            },
            SegmentValueOrigin.RleRun => new ValueDerivation
            {
                Steps =
                [
                    new DerivationStep
                    {
                        Name = "RLE Entry",
                        Value = $"{source.EntryIndex}",
                        Target = context.GetRleEntryTarget(source.EntryIndex)
                    }
                ],
                Result = result
            },
            _ => new ValueDerivation
            {
                Steps = [new DerivationStep { Name = "Value Store", Value = $"{Ordinal}" }],
                Result = result
            }
        };
    }
}

/// <summary>
/// What every row of a segment reads through, held once rather than on each row
/// </summary>
public sealed class SegmentRowContext(SegmentBlob blob,
                                      SegmentDataIdStream dataIds,
                                      Func<long, ValueDerivation?> deriveValue,
                                      bool showDerivation)
{
    public SegmentDataIdStream DataIds { get; } = dataIds;

    public long MinId { get; } = blob.BitpackMinId;

    /// <summary>
    /// The unit the packed value sits in, a unit being what the bit pack region is marked and navigated by
    /// </summary>
    public SegmentNavigationTarget? GetBitpackUnitTarget(int bitpackIndex)
    {
        var perUnit = blob.Bitpack.ValuesPerUnit;

        if (perUnit <= 0 || bitpackIndex < 0)
        {
            return null;
        }

        var offset = blob.BitpackArrayOffset + (bitpackIndex / perUnit * BitpackArray.UnitBytes);

        return new SegmentNavigationTarget(SegmentRegion.BitpackArray, offset);
    }

    public SegmentNavigationTarget? GetRleEntryTarget(int entryIndex)
        => entryIndex < 0
            ? null
            : new SegmentNavigationTarget(SegmentRegion.RleArray,
                                          blob.RleArrayOffset + (entryIndex * blob.RleEntryBytes));

    public Func<long, ValueDerivation?> DeriveValue { get; } = deriveValue;

    /// <summary>
    /// Carried on the context rather than bound from the view, a cell template only reaching its own row
    /// </summary>
    public bool ShowDerivation { get; } = showDerivation;
}
