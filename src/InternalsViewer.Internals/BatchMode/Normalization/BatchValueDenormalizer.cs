using System.Data;

namespace InternalsViewer.Internals.BatchMode.Normalization;

/// <summary>
/// Decodes normalized batch vector values
/// </summary>
public static class BatchValueDenormalizer
{
    private const long RealCarriedMask = 0x1FFFFFFFFFFFFFFF;

    private const long TicksPerScaleTwoUnit = 100000;

    private const int OffsetBits = 11;

    private const int OffsetBias = 1024;

    private const long OffsetMask = (1 << OffsetBits) - 1;

    private const long DictionaryLowMask = 0x7FFF;

    private const int DictionaryHighShift = 33;

    private const long DictionaryHighMask = 0x7FFF8000;

    private const int DictionaryEntryShift = 16;

    private const long DictionaryEntryMask = 0xFFFFFFFF;

    public static BatchValueType GetValueType(BatchValue slot, BatchValueDomain domain)
    {
        if (slot.IsNull)
        {
            return BatchValueType.Null;
        }

        if (slot.IsDeepDataReference)
        {
            return BatchValueType.DeepDataReference;
        }

        return domain == BatchValueDomain.Dictionary
               ? BatchValueType.DictionaryReference
               : BatchValueType.Inline;
    }

    /// <summary>
    /// Gets the Data Id for a dictionary
    /// </summary>
    public static long GetDictionaryDataId(BatchValue slot) => slot.Value >> 16;

    public static int GetDictionaryNumber(BatchValue slot)
        => (int)(((slot.Value >> 1) & DictionaryLowMask) | ((slot.Value >>> DictionaryHighShift) & DictionaryHighMask));

    public static long GetDictionaryEntry(BatchValue slot) => (slot.Value >>> DictionaryEntryShift) & DictionaryEntryMask;

    /// <summary>
    /// Gets the storage value an inline slot holds
    /// </summary>
    public static long GetStorageValue(BatchValue slot, BatchValueDomain domain) => domain switch
    {
        BatchValueDomain.Integer or BatchValueDomain.Numeric 
            => slot.Value >> 1,
        BatchValueDomain.Real 
            => GetRealStorageValue(slot),
        BatchValueDomain.Temporal 
            => throw new NotSupportedException("Temporal slots decode through GetTemporalValue"),
        _ => throw new NotSupportedException($"{domain} slots hold no storage value")
    };

    public static object GetTemporalValue(BatchValue slot, SqlDbType dataType)
    {
        return dataType switch
        {
            SqlDbType.Date
                => DateOnly.FromDayNumber((int)(slot.Value >> 1)),
            SqlDbType.DateTime2
                => new DateTime(slot.Value >> 1),
            SqlDbType.Time
                => new TimeSpan(slot.Value >> 1),
            SqlDbType.DateTimeOffset
                => ToDateTimeOffset(slot.Value >> 1),
            _ => throw new NotSupportedException($"{dataType} slots are not decoded yet")
        };
    }

    private static DateTimeOffset ToDateTimeOffset(long storage)
    {
        var offset = TimeSpan.FromMinutes((int)(storage & OffsetMask) - OffsetBias);

        var utc = new DateTime((storage >> OffsetBits) * TicksPerScaleTwoUnit, DateTimeKind.Utc);

        return new DateTimeOffset(utc).ToOffset(offset);
    }

    private static long GetRealStorageValue(BatchValue slot)
        => slot.Value ^ (((slot.Value >>> 1) ^ slot.Value) & RealCarriedMask);
}
