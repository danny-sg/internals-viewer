using System.Data;
using System.Data.SqlTypes;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Execution.BatchMode.Vectors;

public static class BatchRecordBuilder
{
    public static BatchRecord Build(ExecutionBatch batch, int row)
    {
        var fields = new List<RecordField>(batch.Vectors.Count);

        foreach (var vector in batch.Vectors)
        {
            fields.Add(new ComputedField(vector.Column.Name, ToValue(batch, vector, row)));
        }

        return new BatchRecord(fields, batch.RowGroupId, row);
    }

    public static AccessValue ToValue(ExecutionBatch batch, BatchVector vector, int row)
    {
        var column = vector.Column;

        var slot = vector.Slots[row];

        return BatchSlotDenormalizer.GetValueType(slot, column) switch
        {
            BatchSlotValueType.Null
                => AccessValue.FromNull(column.DataType),
            BatchSlotValueType.DeepDataReference
                => AccessValue.FromBytes(column.DataType, batch.DeepDataContext.Get(slot.Value).ToArray()),
            BatchSlotValueType.DictionaryReference
                => FromDictionary(vector, BatchSlotDenormalizer.GetDictionaryDataId(slot)),
            _ => FromInline(column, slot)
        };
    }

    private static AccessValue FromDictionary(BatchVector vector, long dataId)
        => AccessValueFactory.FromObject(vector.Column.DataType, vector.Source?.GetValueForDataId(dataId));

    private static AccessValue FromInline(BatchColumn column, BatchSlot slot)
        => column.Domain switch
    {
        BatchSlotDomain.Real
            => AccessValue.FromReal(column.DataType,
                                    BitConverter.Int64BitsToDouble(BatchSlotDenormalizer.GetStorageValue(slot, column))),
        BatchSlotDomain.Numeric
            => AccessValue.FromDecimal(column.DataType, ToNumeric(column, slot)),
        BatchSlotDomain.Temporal
            => AccessValue.FromInteger(column.DataType, ToTicks(column, slot)),
        _ => AccessValue.FromInteger(column.DataType, BatchSlotDenormalizer.GetStorageValue(slot, column))
    };

    private static decimal ToNumeric(BatchColumn column, BatchSlot slot)
    {
        var storage = BatchSlotDenormalizer.GetStorageValue(slot, column);

        var magnitude = storage < 0 ? (ulong)-storage : (ulong)storage;

        var numeric = new SqlDecimal(column.Precision,
                                     column.Scale,
                                     storage >= 0,
                                     (int)(magnitude & 0xFFFFFFFF),
                                     (int)(magnitude >> 32),
                                     0,
                                     0);

        return numeric.Value;
    }

    private static long ToTicks(BatchColumn column, BatchSlot slot)
        => BatchSlotDenormalizer.GetTemporalValue(slot, column) switch
    {
        DateTime moment => moment.Ticks,
        TimeSpan span => span.Ticks,
        _ => 0
    };

    private static byte[] Encode(SqlDbType dataType, string text)
        => dataType switch
           {
               SqlDbType.NChar or SqlDbType.NVarChar or SqlDbType.NText
                   => Encoding.Unicode.GetBytes(text),
               _ => Encoding.Latin1.GetBytes(text)
           };
}
