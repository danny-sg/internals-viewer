using System.Data;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.BatchMode.Normalization;

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

    public static BatchValueType GetValueType(BatchValue slot, BatchColumn column)
    {
        if (slot.IsNull)
        {
            return BatchValueType.Null;
        }

        if (slot.IsDeepDataReference)
        {
            return BatchValueType.DeepDataReference;
        }

        return column.Domain == BatchValueDomain.Dictionary
               ? BatchValueType.DictionaryReference
               : BatchValueType.Inline;
    }

    /// <summary>
    /// Gets the Data Id for a dictionary
    /// </summary>
    public static long GetDictionaryDataId(BatchValue slot) => slot.Value >> 16;

    /// <summary>
    /// Gets the storage value an inline slot holds
    /// </summary>
    public static long GetStorageValue(BatchValue slot, BatchColumn column) => column.Domain switch
    {
        BatchValueDomain.Integer or BatchValueDomain.Numeric 
            => slot.Value >> 1,
        BatchValueDomain.Real 
            => GetRealStorageValue(slot),
        BatchValueDomain.Temporal 
            => throw new NotSupportedException("Temporal slots decode through GetTemporalValue"),
        _ => throw new NotSupportedException($"{column.Domain} slots hold no storage value")
    };

    public static object GetTemporalValue(BatchValue slot, BatchColumn column)
    {
        return column.DataType switch
        {
            SqlDbType.DateTime2
                => new DateTime(slot.Value >> 1),
            SqlDbType.Time
                => new TimeSpan(slot.Value >> 1),
            SqlDbType.DateTimeOffset
                => ToDateTimeOffset(slot.Value >> 1),
            _ => throw new NotSupportedException($"{column.DataType} slots are not decoded yet")
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
