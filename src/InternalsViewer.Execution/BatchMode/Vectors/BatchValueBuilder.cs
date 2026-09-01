using System.Data;
using System.Data.SqlTypes;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Execution.BatchMode.Vectors;

public static class BatchValueBuilder
{
    public static BatchValue FromField(BatchColumn column, RecordField field, IDeepDataContext deepData)
    {
        if (field.IsNull)
        {
            return BatchValueNormalizer.Null;
        }

        if (column.Domain == BatchValueDomain.Temporal && TryTemporalField(column, field, out var temporal))
        {
            return temporal;
        }

        if (column.Domain == BatchValueDomain.Numeric
            && BatchValueNormalizer.TryNormalize(ToNumeric(column, field.GetValue<decimal>()), out var numeric))
        {
            return numeric;
        }

        return FromValue(column, AccessValueFactory.FromField(field), deepData);
    }

    public static BatchValue FromValue(BatchColumn column, AccessValue value, IDeepDataContext deepData)
    {
        if (value.IsNull)
        {
            return BatchValueNormalizer.Null;
        }

        if (TryNormalize(column, value, out var slot))
        {
            return slot;
        }

        return value.Data.IsEmpty
            ? BatchValueNormalizer.Null
            : new BatchValue(deepData.Store(value.Data.Span));
    }

    private static bool TryNormalize(BatchColumn column, AccessValue value, out BatchValue slot)
    {
        switch (column.Domain)
        {
            case BatchValueDomain.Integer when value.Type == AccessValueType.Integer:
                return BatchValueNormalizer.TryNormalize(value.Numeric, out slot);

            case BatchValueDomain.Real when value.Type == AccessValueType.Real:
                return BatchValueNormalizer.TryNormalize(value.Real, out slot);

            case BatchValueDomain.Numeric when value.Type == AccessValueType.Decimal:
                return BatchValueNormalizer.TryNormalize(ToNumeric(column, value.ToDecimal()), out slot);

            case BatchValueDomain.Temporal when value.Type == AccessValueType.Integer:
                return TryTemporalTicks(column, value.Numeric, out slot);

            default:
                slot = BatchValueNormalizer.Null;

                return false;
        }
    }

    private static bool TryTemporalField(BatchColumn column, RecordField field, out BatchValue slot)
        => column.DataType switch
        {
            SqlDbType.DateTime2 
                => BatchValueNormalizer.TryNormalize(field.GetValue<DateTime>(), out slot),
            SqlDbType.Time 
                => BatchValueNormalizer.TryNormalize(field.GetValue<TimeSpan>(), out slot),
            SqlDbType.DateTimeOffset 
                => BatchValueNormalizer.TryNormalize(field.GetValue<DateTimeOffset>(), out slot),
            _ => Fail(out slot)
        };

    private static bool TryTemporalTicks(BatchColumn column, long ticks, out BatchValue slot)
        => column.DataType switch
        {
            SqlDbType.DateTime2 
                => BatchValueNormalizer.TryNormalize(new DateTime(ticks), out slot),
            SqlDbType.Time
                => BatchValueNormalizer.TryNormalize(new TimeSpan(ticks), out slot),
            SqlDbType.DateTimeOffset
                => BatchValueNormalizer.TryNormalize(new DateTimeOffset(ticks, TimeSpan.Zero), out slot),
            _ => Fail(out slot)
        };

    private static SqlDecimal ToNumeric(BatchColumn column, decimal value)
        => SqlDecimal.ConvertToPrecScale(new SqlDecimal(value), column.Precision, column.Scale);

    private static bool Fail(out BatchValue slot)
    {
        slot = BatchValueNormalizer.Null;

        return false;
    }
}
