using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Paged value array a store by value segment holds in place of an RLE and bit pack pair
/// </summary>
public sealed class SegmentVariableLengthData : DataStructure
{
    public const int HeaderSize = 24;

    /// <summary>
    /// Low bit of a stored value is reserved, so the scaled value is the stored one halved
    /// </summary>
    public const int ReservedBits = 1;

    [DataStructureItem(ItemType.VariableLengthDataHeader)]
    public SegmentVariableLengthDataHeader Header { get; set; } = new();

    [DataStructureItem(ItemType.PageSizeArray)]
    public SegmentPageSizeArray PageSizeArray { get; set; } = new();

    public int ValueCount => Header.ValueCount;

    public int MaxStringSize => Header.MaxStringSize;

    /// <summary>
    /// Element size of the page size array rather than of a value
    /// </summary>
    public int ElementSize => PageSizeArray.ElementSize;

    public int PageCount => PageSizeArray.ElementCount;

    /// <summary>
    /// Where the store begins in the segment blob, its fields being marked against the blob rather than itself
    /// </summary>
    public int Offset { get; set; }

    public int[] PageSizes { get; set; } = [];

    public SegmentValuePage[] Pages { get; set; } = [];

    /// <summary>
    /// Whether the values are wider than an integer, which the numeric encodings have no way to carry
    /// </summary>
    public bool IsWide => Pages.Length > 0 && Pages[0].IsWide;

    /// <summary>
    /// Records the header fields and the page size array that follows them
    /// </summary>
    public void Mark()
    {
        MarkProperty(nameof(Header), Offset, SegmentVariableLengthDataHeader.Size);

        MarkProperty(nameof(PageSizeArray), PageSizeArray.Offset, PageSizeArray.TotalSize);

        Header.Mark();

        PageSizeArray.Mark();
    }

    /// <summary>
    /// Value as stored, for a width the integer path cannot take
    /// </summary>
    public ReadOnlyMemory<byte> GetValueBytes(int ordinal)
    {
        var (page, index) = Locate(ordinal);

        return Pages[page].GetValueBytes(index);
    }

    public bool IsNull(int ordinal)
    {
        var (page, index) = Locate(ordinal);

        return Pages[page].IsNull(index);
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
    public int GetPageIndex(int ordinal) => Locate(ordinal).Page;

    /// <summary>
    /// Where the value a page and slot pair addresses starts in the segment blob
    /// </summary>
    public int GetValueOffset(int page, int slot)
        => page >= 0 && page < Pages.Length ? Pages[page].GetValueOffset(slot) : Offset;

    /// <summary>
    /// Ordinal a page and slot pair addresses, which is how an RLE run names where its values start
    /// </summary>
    public int GetOrdinal(int page, int slot)
    {
        var ordinal = slot;

        for (var i = 0; i < page && i < Pages.Length; i++)
        {
            ordinal += Pages[i].ValueCount;
        }

        return ordinal;
    }

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
