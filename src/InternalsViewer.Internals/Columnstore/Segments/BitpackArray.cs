using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Fixed width values packed into sixty four bit Units
/// </summary>
/// <remarks>
/// Filled from the least significant bit upward
/// </remarks>
public readonly struct BitpackArray(ReadOnlyMemory<byte> data, int entrySizeBits, int unitCount, long minId)
{
    public const int UnitBits = 64;

    public const int UnitBytes = 8;

    public ReadOnlyMemory<byte> Data { get; } = data;

    public int EntrySizeBits { get; } = entrySizeBits;

    public int UnitCount { get; } = unitCount;

    /// <summary>
    /// Reserved data id floor added back to every packed value
    /// </summary>
    public long MinId { get; } = minId;

    public int ValuesPerUnit => EntrySizeBits > 0 ? UnitBits / EntrySizeBits : 0;

    public int Count => ValuesPerUnit * UnitCount;

    public long this[int index]
    {
        get
        {
            var perUnit = ValuesPerUnit;

            if (perUnit == 0 || (uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var unit = BitConverter.ToUInt64(Data.Span.Slice(index / perUnit * UnitBytes, UnitBytes));

            var shift = EntrySizeBits * (index % perUnit);

            var mask = EntrySizeBits == UnitBits ? ulong.MaxValue : (1UL << EntrySizeBits) - 1;

            return (long)((unit >> shift) & mask) + MinId;
        }
    }

    /// <summary>
    /// Bit address of a packed value relative to the start of the bit pack array
    /// </summary>
    public BitSpan GetSpan(int index)
    {
        var perUnit = ValuesPerUnit;

        var bitOffset = (index / perUnit * UnitBits) + (EntrySizeBits * (index % perUnit));

        return new BitSpan(bitOffset, EntrySizeBits);
    }
}
