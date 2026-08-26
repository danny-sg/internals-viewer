using System.Data;
using System.Data.SqlTypes;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Iterators.BatchMode.DataAccess;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.BatchMode;

/// <summary>
/// Iterator that converts between Batch Mode and Row Mode
/// </summary>
/// <remarks>
/// Reads per batch, materializes the batch vector to rows, then emits each row as a Row Mode iterator.
///
/// This does exist as CQScanBatchHelper, but it's not visible on query plans
/// </remarks>
public sealed class BatchToRowIterator(ColumnstoreScanIterator source) : IteratorBase
{
    public override PageAddress? CurrentPageAddress => null;

    public override AccessStrategy? Strategy => null;

    private ExecutionBatch? Batch { get; set; }

    private int Position { get; set; }

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var adapter = definition.Expect<BatchToRowDefinition>();

        await PrepareAsync(definition, context, cancellationToken);

        Batch = null;

        Position = 0;

        await source.OpenAsync(adapter.Batch, context, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        while (true)
        {
            if (Batch is null || Position >= Batch.RowCount)
            {
                Batch = await source.GetNextBatchAsync(cancellationToken);

                Position = 0;

                if (Batch is null)
                {
                    CurrentRow = null;

                    IsComplete = true;

                    return null;
                }
            }

            Position = Batch.SelectionBitmap.GetNextSetIndex(Position);

            if (Position < 0)
            {
                Position = Batch.RowCount;

                continue;
            }

            CurrentRow = Materialise(Batch, Position);

            Position++;

            return CurrentRow;
        }
    }

    public override async Task CloseAsync()
    {
        await source.CloseAsync();

        Batch = null;

        await base.CloseAsync();
    }

    private static BatchRecord Materialise(ExecutionBatch batch, int position)
    {
        var fields = new List<RecordField>(batch.Vectors.Count);

        foreach (var vector in batch.Vectors)
        {
            fields.Add(new ComputedField(vector.Column.Name, ToValue(batch, vector, position)));
        }

        return new BatchRecord(fields, batch.RowGroupId, position);
    }

    private static AccessValue ToValue(ExecutionBatch batch, BatchVector vector, int position)
    {
        var column = vector.Column;

        var slot = vector.Slots[position];

        return BatchSlotDenormalizer.GetValueType(slot, column) switch
        {
            BatchSlotValueType.Null
                => AccessValue.FromNull(column.DataType),
            BatchSlotValueType.DeepDataReference
                => AccessValue.FromBytes(column.DataType, batch.DeepData.Get(slot.Value).ToArray()),
            BatchSlotValueType.DictionaryReference
                => FromDictionary(vector, BatchSlotDenormalizer.GetDictionaryDataId(slot)),
            _ => FromInline(column, slot)
        };
    }

    private static AccessValue FromDictionary(BatchVector vector, long dataId)
    {
        var column = vector.Column;

        return vector.Source?.GetValueForDataId(dataId) switch
        {
            null 
                => AccessValue.FromNull(column.DataType),
            byte[] bytes 
                => AccessValue.FromBytes(column.DataType, bytes),
            string text 
                => AccessValue.FromBytes(column.DataType, Encode(column.DataType, text)),
            long number 
                => AccessValue.FromInteger(column.DataType, number),
            double number 
                => AccessValue.FromReal(column.DataType, number),
            SqlDecimal number 
                => AccessValue.FromDecimal(column.DataType, number.Value),
            decimal number 
                => AccessValue.FromDecimal(column.DataType, number),
            var other 
                => AccessValue.FromBytes(column.DataType, Encode(column.DataType, other.ToString() ?? string.Empty))
        };
    }

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
