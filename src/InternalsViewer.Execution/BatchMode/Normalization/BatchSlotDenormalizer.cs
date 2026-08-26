using System.Data;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.BatchMode.Normalization;

/// <summary>
/// Decodes normalized batch vector values
/// </summary>
public static class BatchSlotDenormalizer
{
    private const long RealCarriedMask = 0x1FFFFFFFFFFFFFFF;

    private const long TicksPerScaleTwoUnit = 100000;

    private const int OffsetBits = 11;

    private const int OffsetBias = 1024;

    private const long OffsetMask = (1 << OffsetBits) - 1;

    public static BatchSlotValueType GetValueType(BatchSlot slot, BatchColumn column)
    {
        if (slot.IsNull)
        {
            return BatchSlotValueType.Null;
        }

        if (slot.IsDeepDataReference)
        {
            return BatchSlotValueType.DeepDataReference;
        }

        return column.Domain == BatchSlotDomain.Dictionary
               ? BatchSlotValueType.DictionaryReference
               : BatchSlotValueType.Inline;
    }

    /// <summary>
    /// Gets the Data Id for a dictionary
    /// </summary>
    public static long GetDictionaryDataId(BatchSlot slot) => slot.Value >> 16;

    /// <summary>
    /// Gets the storage value an inline slot holds
    /// </summary>
    public static long GetStorageValue(BatchSlot slot, BatchColumn column) => column.Domain switch
    {
        BatchSlotDomain.Integer or BatchSlotDomain.Numeric 
            => slot.Value >> 1,
        BatchSlotDomain.Real 
            => GetRealStorageValue(slot),
        BatchSlotDomain.Temporal 
            => throw new NotSupportedException("Temporal slots decode through GetTemporalValue"),
        _ => throw new NotSupportedException($"{column.Domain} slots hold no storage value")
    };

    public static object GetTemporalValue(BatchSlot slot, BatchColumn column)
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

    private static long GetRealStorageValue(BatchSlot slot)
        => slot.Value ^ (((slot.Value >>> 1) ^ slot.Value) & RealCarriedMask);
}
