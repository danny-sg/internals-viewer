using System.Data;
using System.Data.SqlTypes;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Execution.BatchMode.Vectors;

public static class BatchSlotBuilder
{
    public static BatchSlot FromField(BatchColumn column, RecordField field, IDeepDataContext deepData)
    {
        if (field.IsNull)
        {
            return BatchSlotNormalizer.Null;
        }

        if (column.Domain == BatchSlotDomain.Temporal && TryTemporalField(column, field, out var temporal))
        {
            return temporal;
        }

        if (column.Domain == BatchSlotDomain.Numeric
            && BatchSlotNormalizer.TryNormalize(ToNumeric(column, field.GetValue<decimal>()), out var numeric))
        {
            return numeric;
        }

        return FromValue(column, AccessValueFactory.FromField(field), deepData);
    }

    public static BatchSlot FromValue(BatchColumn column, AccessValue value, IDeepDataContext deepData)
    {
        if (value.IsNull)
        {
            return BatchSlotNormalizer.Null;
        }

        if (TryNormalize(column, value, out var slot))
        {
            return slot;
        }

        return value.Data.IsEmpty
            ? BatchSlotNormalizer.Null
            : new BatchSlot(deepData.Store(value.Data.Span));
    }

    private static bool TryNormalize(BatchColumn column, AccessValue value, out BatchSlot slot)
    {
        switch (column.Domain)
        {
            case BatchSlotDomain.Integer when value.Type == AccessValueType.Integer:
                return BatchSlotNormalizer.TryNormalize(value.Numeric, out slot);

            case BatchSlotDomain.Real when value.Type == AccessValueType.Real:
                return BatchSlotNormalizer.TryNormalize(value.Real, out slot);

            case BatchSlotDomain.Numeric when value.Type == AccessValueType.Decimal:
                return BatchSlotNormalizer.TryNormalize(ToNumeric(column, value.ToDecimal()), out slot);

            case BatchSlotDomain.Temporal when value.Type == AccessValueType.Integer:
                return TryTemporalTicks(column, value.Numeric, out slot);

            default:
                slot = BatchSlotNormalizer.Null;

                return false;
        }
    }

    private static bool TryTemporalField(BatchColumn column, RecordField field, out BatchSlot slot)
        => column.DataType switch
        {
            SqlDbType.DateTime2 
                => BatchSlotNormalizer.TryNormalize(field.GetValue<DateTime>(), out slot),
            SqlDbType.Time 
                => BatchSlotNormalizer.TryNormalize(field.GetValue<TimeSpan>(), out slot),
            SqlDbType.DateTimeOffset 
                => BatchSlotNormalizer.TryNormalize(field.GetValue<DateTimeOffset>(), out slot),
            _ => Fail(out slot)
        };

    private static bool TryTemporalTicks(BatchColumn column, long ticks, out BatchSlot slot)
        => column.DataType switch
        {
            SqlDbType.DateTime2 
                => BatchSlotNormalizer.TryNormalize(new DateTime(ticks), out slot),
            SqlDbType.Time
                => BatchSlotNormalizer.TryNormalize(new TimeSpan(ticks), out slot),
            SqlDbType.DateTimeOffset
                => BatchSlotNormalizer.TryNormalize(new DateTimeOffset(ticks, TimeSpan.Zero), out slot),
            _ => Fail(out slot)
        };

    private static SqlDecimal ToNumeric(BatchColumn column, decimal value)
        => SqlDecimal.ConvertToPrecScale(new SqlDecimal(value), column.Precision, column.Scale);

    private static bool Fail(out BatchSlot slot)
    {
        slot = BatchSlotNormalizer.Null;

        return false;
    }
}
