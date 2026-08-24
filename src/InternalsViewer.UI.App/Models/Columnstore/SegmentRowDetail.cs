using System;
using InternalsViewer.Internals.Columnstore.Decoding;

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
    private bool _sourceResolved;

    private SegmentDataIdSource Source
    {
        get
        {
            if (!_sourceResolved)
            {
                field = context.DataIds.GetSource(Ordinal);

                _sourceResolved = true;
            }

            return field;
        }
    }

    public int Ordinal { get; } = ordinal;

    public long DataId => Source.DataId;

    public string OriginDescription => Source.Origin switch
    {
        SegmentValueOrigin.BitPack => "Bit Pack",
        SegmentValueOrigin.RleRun => "RLE Run",
        SegmentValueOrigin.VariableLengthData => "Variable Length Data",
        _ => string.Empty
    };

    /// <summary>
    /// The bits as they were stored, which only a bit packed row holds separately from its data id
    /// </summary>
    public string PackedDescription
        => field ??= Source.Origin == SegmentValueOrigin.BitPack
                     ? (DataId - context.MinId).ToString()
                     : string.Empty;

    public ValueDerivation DataIdDerivation => field ??= BuildDataIdDerivation();

    public ValueDerivation? ValueDerivation => field ??= context.DeriveValue(Ordinal, DataId);

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

        var result = source.DataId.ToString();

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
                Steps = [new DerivationStep { Name = "Variable Length Data", Value = $"{Ordinal}" }],
                Result = result
            }
        };
    }
}