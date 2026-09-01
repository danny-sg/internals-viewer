using System.Data;
using System.Data.SqlTypes;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Execution.BatchMode.Vectors;

public static class BatchRecordBuilder
{
    public const string RowGroupKeyColumn = "Row Group";

    public const string DataIdSuffix = " Data Id";

    public static BatchRecord Build(ExecutionBatch batch, int row)
    {
        var fields = new List<RecordField>(batch.Vectors.Count);

        foreach (var vector in batch.Vectors)
        {
            fields.Add(new ComputedField(vector.Column.Name, ToValue(batch, vector, row)));
        }

        return new BatchRecord(fields, batch.RowGroupId, row);
    }

    public static AccessKey BuildLocalKey(ExecutionBatch batch, int row, IReadOnlyList<string> groupBy)
    {
        var values = new AccessValue[groupBy.Count + 1];

        values[0] = AccessValue.FromInteger(SqlDbType.Int, batch.RowGroupId).WithColumnName(RowGroupKeyColumn);

        for (var index = 0; index < groupBy.Count; index++)
        {
            var column = groupBy[index];

            values[index + 1] = ToLocalValue(batch, Find(batch, column), row).WithColumnName(column + DataIdSuffix);
        }

        return new AccessKey([.. values]);
    }

    public static AccessValue ToLocalValue(ExecutionBatch batch, BatchVector? vector, int row)
    {
        if (vector is null)
        {
            return AccessValue.Null;
        }

        var slot = vector[row];

        return BatchValueDenormalizer.GetValueType(slot, vector.Column) == BatchValueType.DictionaryReference
               ? AccessValue.FromInteger(SqlDbType.BigInt, BatchValueDenormalizer.GetDictionaryDataId(slot))
               : ToValue(batch, vector, row);
    }

    public static AccessValue ToValue(ExecutionBatch batch, BatchVector vector, int row)
    {
        var column = vector.Column;

        var slot = vector[row];

        return BatchValueDenormalizer.GetValueType(slot, column) switch
        {
            BatchValueType.Null
                => AccessValue.FromNull(column.DataType),
            BatchValueType.DeepDataReference
                => AccessValue.FromBytes(column.DataType, batch.DeepDataContext.Get(slot.Value).ToArray()),
            BatchValueType.DictionaryReference
                => FromDictionary(vector, BatchValueDenormalizer.GetDictionaryDataId(slot)),
            _ => FromInline(column, slot)
        };
    }

    private static BatchVector? Find(ExecutionBatch batch, string column)
    {
        foreach (var vector in batch.Vectors)
        {
            if (string.Equals(vector.Column.Name, column, StringComparison.OrdinalIgnoreCase))
            {
                return vector;
            }
        }

        return null;
    }

    private static AccessValue FromDictionary(BatchVector vector, long dataId)
        => AccessValueFactory.FromObject(vector.Column.DataType, vector.Source?.GetValueForDataId(dataId));

    private static AccessValue FromInline(BatchColumn column, BatchValue slot)
        => column.Domain switch
    {
        BatchValueDomain.Real
            => AccessValue.FromReal(column.DataType,
                                    BitConverter.Int64BitsToDouble(BatchValueDenormalizer.GetStorageValue(slot, column))),
        BatchValueDomain.Numeric
            => AccessValue.FromDecimal(column.DataType, ToNumeric(column, slot)),
        BatchValueDomain.Temporal
            => AccessValue.FromInteger(column.DataType, ToTicks(column, slot)),
        _ => AccessValue.FromInteger(column.DataType, BatchValueDenormalizer.GetStorageValue(slot, column))
    };

    private static decimal ToNumeric(BatchColumn column, BatchValue slot)
    {
        var storage = BatchValueDenormalizer.GetStorageValue(slot, column);

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

    private static long ToTicks(BatchColumn column, BatchValue slot)
        => BatchValueDenormalizer.GetTemporalValue(slot, column) switch
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
