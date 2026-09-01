using System.Data.SqlTypes;

namespace InternalsViewer.Execution.BatchMode.Normalization;

/// <summary>
/// Normalizes a value into a Vector Batch Slot value
/// </summary>
/// <remarks>
/// SQL Server converts values of different type into a 64 bit value via normalization.
///
/// This allows batch mode to take advantage of SIMD processing and vectorization, CPU cache optimizations, and means non-dictionary values
/// can be compared, sorted, and grouped directly without decoding/transformation.
///
/// Types and values that are not representable in a normalized space have a low bit = 1 and are instead represented by a reference to the
/// deep data store, where the value is stored in its original form - see <see cref="BatchDeepDataStore"/>.
///
/// NULL is represented by a special value of 1 (tag set, payload zero).
///
/// See <see href="https://sqlperformance.com/2019/09/sql-performance/batch-mode-normalization-performance"/>
/// </remarks>
public static class BatchValueNormalizer
{
    private const long RealPayloadMask = 0x0FFFFFFFFFFFFFFF;

    private const long RealSignAndExponentMask = unchecked((long)0xE000000000000000);

    private const long TicksPerScaleTwoUnit = 100000;

    private const int OffsetBits = 11;

    private const int OffsetBias = 1024;

    public static BatchValue Null { get; } = new(1);

    /// <summary>
    /// Normalize integer
    /// </summary>
    /// <remarks>
    /// Shifts the value left by 1 bit to make room for the tag bit. Equivalent of doubling the number using binary arithmetic.
    ///
    /// Check is made to ensure the value shifted right by 1 bit gives the original value ensuring that the value is representable in a
    /// normalized space.
    /// </remarks>
    public static bool TryNormalize(long value, out BatchValue slot)
    {
        var normalized = value << 1;

        if (normalized >> 1 != value)
        {
            slot = Null;

            return false;
        }

        slot = new BatchValue(normalized);

        return true;
    }

    /// <summary>
    /// Normalize double
    /// </summary>
    /// <remarks>
    /// The sign and the top two exponent bits are left where they are and only the low 60 bits are shifted left by 1 to make room for the
    /// tag bit. Bit 60 falls off the top of that shift and is not stored.
    ///
    /// Check is made that bits 60 and 61 hold the same value, bit 60 being restored from bit 61 when the value is read back. Failing that
    /// check is a band of the exponent range rather than a magnitude limit, so ordinary values and the very largest both normalize while
    /// magnitudes roughly between 1e-231 and 1e-77, or between 1e77 and 1e231, do not.
    /// </remarks>
    public static bool TryNormalize(double value, out BatchValue slot)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);

        var exponentBits = (bits >>> 60) & 3;

        if (exponentBits is not (0 or 3))
        {
            slot = Null;

            return false;
        }

        slot = new BatchValue(((bits & RealPayloadMask) << 1) | (bits & RealSignAndExponentMask));

        return true;
    }

    /// <summary>
    /// Normalize decimal
    /// </summary>
    /// <remarks>
    /// The sign and magnitude form is folded back into one scaled integer and normalized as an integer, the decimal point coming from the
    /// column scale rather than from anything held in the slot. A decimal(38,9) of -999999999.999999999 normalizes as the integer
    /// -999999999999999999.
    ///
    /// Check is made that the magnitude uses no more than the low two 32 bit words and still survives the doubling, which allows 18
    /// decimal digits or fewer whatever precision the column declares.
    /// </remarks>
    public static bool TryNormalize(SqlDecimal value, out BatchValue slot)
    {
        if (value.IsNull)
        {
            slot = Null;

            return true;
        }

        var data = value.Data;

        if (data[2] != 0 || data[3] != 0)
        {
            slot = Null;

            return false;
        }

        var magnitude = (uint)data[0] | ((ulong)(uint)data[1] << 32);

        if (magnitude > long.MaxValue)
        {
            slot = Null;

            return false;
        }

        return TryNormalize(value.IsPositive ? (long)magnitude : -(long)magnitude, out slot);
    }

    /// <summary>
    /// Normalizes a DateTime value
    /// </summary>
    /// <remarks>
    /// Treats the DateTime as a long of ticks and normalizes it as an integer.
    /// </remarks>
    public static bool TryNormalize(DateTime value, out BatchValue slot) 
        => TryNormalize(value.Ticks, out slot);

    /// <summary>
    /// Normalizes a TimeSpan value
    /// </summary>
    /// <remarks>
    /// Treats the TimeSpan as a long of ticks and normalizes it as an integer.
    /// </remarks>
    public static bool TryNormalize(TimeSpan value, out BatchValue slot) 
        => TryNormalize(value.Ticks, out slot);

    /// <summary>
    /// Normalizes a DateTimeOffset value
    /// </summary>
    /// <remarks>
    /// Where time and datetime2 rescale to their maximum scale of 7, datetimeoffset rescales to a scale of 2, so the instant is held in
    /// hundredths of a second rather than in 100 nanosecond ticks.
    ///
    /// A datetimeoffset carries both an instant and the offset it was written in, and a single integer has to hold the pair. The instant
    /// is kept as UTC so that two values written in different offsets still compare and sort against each other, and the offset is carried
    /// in the low bits so it can be handed back unchanged. Offsets run from -14:00 to +14:00, so the minutes are biased by 1024 to keep
    /// them positive within 11 bits.
    ///
    /// Check is made that the instant is a whole number of hundredths. This is a precision test rather than the range test the integer
    /// overload makes - a datetimeoffset(7) holding anything finer goes to deep data whatever its magnitude.
    ///
    /// Note: the split between the instant and the offset is this implementation's own. SQL Server rescales to a scale of 2 in the same
    /// way, but its internal layout of the two has not been established, so a raw slot value here will not match the engine's.
    /// </remarks>
    public static bool TryNormalize(DateTimeOffset value, out BatchValue slot)
    {
        if (value.UtcTicks % TicksPerScaleTwoUnit != 0)
        {
            slot = Null;

            return false;
        }

        var offset = (long)value.Offset.TotalMinutes + OffsetBias;

        return TryNormalize(((value.UtcTicks / TicksPerScaleTwoUnit) << OffsetBits) | offset, out slot);
    }

    /// <summary>
    /// Normalizes a decoded value whose type is only known at run time
    /// </summary>
    public static bool TryNormalizeValue(object? value, out BatchValue slot)
    {
        switch (value)
        {
            case null:
                slot = Null;
                return true;
            case bool flag:
                return TryNormalize(flag ? 1L : 0L, out slot);
            case byte number:
                return TryNormalize(number, out slot);
            case short number:
                return TryNormalize(number, out slot);
            case int number:
                return TryNormalize(number, out slot);
            case long number:
                return TryNormalize(number, out slot);
            case float number:
                return TryNormalize(number, out slot);
            case double number:
                return TryNormalize(number, out slot);
            case decimal number:
                return TryNormalize(new SqlDecimal(number), out slot);
            case SqlDecimal number:
                return TryNormalize(number, out slot);
            case DateTime moment:
                return TryNormalize(moment, out slot);
            case DateTimeOffset moment:
                return TryNormalize(moment, out slot);
            case TimeSpan span:
                return TryNormalize(span, out slot);
            default:
                slot = Null;
                return false;
        }
    }

    /// <summary>
    /// Normalizes a dictionary Data Id
    /// </summary>
    /// <remarks>
    /// Shifts the Data Id left by 16 bits to normalize it for use as a dictionary slot. This moves the Data Id 2 bytes to the left, leaving
    /// the tag and the low bits clear to mark it as a dictionary reference rather than a deep data reference.
    /// </remarks>
    public static BatchValue FromDictionaryDataId(long dataId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dataId);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(dataId, int.MaxValue);

        return new BatchValue(dataId << 16);
    }
}
