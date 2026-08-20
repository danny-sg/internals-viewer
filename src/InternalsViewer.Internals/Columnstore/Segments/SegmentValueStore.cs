using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Paged value array a store by value segment holds in place of an RLE and bit pack pair
/// </summary>
public sealed class SegmentValueStore : DataStructure
{
    public const int HeaderSize = 24;

    /// <summary>
    /// Low bit of a stored value is reserved, so the scaled value is the stored one halved
    /// </summary>
    public const int ReservedBits = 1;

    [DataStructureItem(ItemType.ValueStoreUnknown)]
    public int Unknown00 { get; set; }

    [DataStructureItem(ItemType.ValueStoreValueCount)]
    public int ValueCount { get; set; }

    [DataStructureItem(ItemType.ValueStoreMaxStringSize)]
    public int MaxStringSize { get; set; }

    [DataStructureItem(ItemType.ValueStoreSubLobType)]
    public SubLobType SubLobType { get; set; }

    /// <summary>
    /// Element size of the page size array rather than of a value
    /// </summary>
    [DataStructureItem(ItemType.ValueStoreElementSize)]
    public int ElementSize { get; set; }

    [DataStructureItem(ItemType.ValueStorePageCount)]
    public int PageCount { get; set; }

    /// <summary>
    /// Where the store begins in the segment blob, its fields being marked against the blob rather than itself
    /// </summary>
    public int Offset { get; set; }

    [DataStructureItem(ItemType.ValueStorePageSizes)]
    public int[] PageSizes { get; set; } = [];

    public SegmentValuePage[] Pages { get; set; } = [];

    /// <summary>
    /// Records the header fields and the page size array that follows them
    /// </summary>
    public void Mark()
    {
        MarkProperty(nameof(Unknown00), Offset, 4);
        MarkProperty(nameof(ValueCount), Offset + 0x04, 4);
        MarkProperty(nameof(MaxStringSize), Offset + 0x08, 4);
        MarkProperty(nameof(SubLobType), Offset + 0x0C, 4);
        MarkProperty(nameof(ElementSize), Offset + 0x10, 4);
        MarkProperty(nameof(PageCount), Offset + 0x14, 4);

        if (PageCount > 0 && ElementSize > 0)
        {
            MarkProperty(nameof(PageSizes), Offset + HeaderSize, PageCount * ElementSize);
        }
    }

    public long GetRawValue(int ordinal)
    {
        var (page, index) = Locate(ordinal);

        return Pages[page].GetRawValue(index);
    }

    /// <summary>
    /// Scaled value by ordinal
    /// </summary>
    public long GetValue(int ordinal) => GetRawValue(ordinal) >> ReservedBits;

    /// <summary>
    /// The page an ordinal falls on, which is the closest a stored value comes to having a place in the blob
    /// </summary>
    /// <remarks>
    /// A value is not addressable on its own. The page holds a compressed payload and the value only exists once
    /// that has been expanded, so the page is the tightest range of the blob a row can be pointed at.
    /// </remarks>
    public int GetPageIndex(int ordinal) => Locate(ordinal).Page;

    private (int Page, int Index) Locate(int ordinal)
    {
        if ((uint)ordinal >= (uint)ValueCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var remaining = ordinal;

        for (var i = 0; i < Pages.Length; i++)
        {
            if (remaining < Pages[i].ValueCount)
            {
                return (i, remaining);
            }

            remaining -= Pages[i].ValueCount;
        }

        throw new ArgumentOutOfRangeException(nameof(ordinal));
    }
}
