using System;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// What every row of a segment reads through, held once rather than on each row
/// </summary>
public sealed class SegmentRowContext(SegmentBlob blob,
                                      SegmentDataIdStream dataIds,
                                      Func<int, long, ValueDerivation?> deriveValue,
                                      bool showDerivation)
{
    public SegmentDataIdStream DataIds { get; } = dataIds;

    public long MinId { get; } = blob.Header.BitpackMinId;

    public bool IsWideStore => blob.VariableLengthData is { IsWide: true };

    public Func<int, long, ValueDerivation?> DeriveValue { get; } = deriveValue;

    /// <summary>
    /// Carried on the context rather than bound from the view, a cell template only reaching its own row
    /// </summary>
    public bool ShowDerivation { get; } = showDerivation;

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

        var offset = blob.Header.BitpackArrayOffset + (bitpackIndex / perUnit * BitpackArray.UnitBytes);

        return new SegmentNavigationTarget(SegmentRegion.BitpackArray, offset);
    }

    public SegmentNavigationTarget? GetRleEntryTarget(int entryIndex)
        => entryIndex < 0
            ? null
            : new SegmentNavigationTarget(SegmentRegion.RleArray,
                blob.Header.RleArrayOffset + (entryIndex * blob.Header.RleEntrySize));

    /// <summary>
    /// Address of a stored value, a wide one having no data id of its own to show instead
    /// </summary>
    public string? GetPageSlotDescription(int valueOrdinal)
        => blob.VariableLengthData is { } store && valueOrdinal >= 0 && valueOrdinal < store.ValueCount
            ? store.GetPageSlot(valueOrdinal).ToString()
            : null;

    /// <summary>
    /// Where a stored value sits, a compressed page having only the page itself to point at
    /// </summary>
    public SegmentNavigationTarget? GetValueTarget(int valueOrdinal)
    {
        if (blob.VariableLengthData is not { } store || valueOrdinal < 0 || valueOrdinal >= store.ValueCount)
        {
            return null;
        }

        var address = store.GetPageSlot(valueOrdinal);

        return new SegmentNavigationTarget(SegmentRegion.VariableLengthData,
                                           store.GetValueOffset(address.Page, address.Slot));
    }
}