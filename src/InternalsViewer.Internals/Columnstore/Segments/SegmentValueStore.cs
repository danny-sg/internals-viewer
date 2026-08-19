using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Paged value array a store by value segment holds in place of an RLE and bit pack pair
/// </summary>
public sealed class SegmentValueStore
{
    public const int HeaderSize = 24;

    /// <summary>
    /// Low bit of a stored value is reserved, so the scaled value is the stored one halved
    /// </summary>
    public const int ReservedBits = 1;

    public int Unknown00 { get; set; }

    public int ValueCount { get; set; }

    public int MaxStringSize { get; set; }

    public SubLobType SubLobType { get; set; }

    /// <summary>
    /// Element size of the page size array rather than of a value
    /// </summary>
    public int ElementSize { get; set; }

    public int[] PageSizes { get; set; } = [];

    public SegmentValuePage[] Pages { get; set; } = [];

    public long GetRawValue(int ordinal)
    {
        var (page, index) = Locate(ordinal);

        return Pages[page].GetRawValue(index);
    }

    /// <summary>
    /// Scaled value by ordinal
    /// </summary>
    public long GetValue(int ordinal) => GetRawValue(ordinal) >> ReservedBits;

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
