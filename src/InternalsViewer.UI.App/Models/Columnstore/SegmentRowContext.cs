using System;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore;

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