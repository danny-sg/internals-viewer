using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Common;

/// <summary>
/// Packs rows into a batch
/// </summary>
public static class BatchPacker
{
    public static ExecutionBatch Pack(IEnumerable<IRecord> rows, ExecutionBatch? reuse)
    {
        var batch = reuse;

        var count = 0;

        foreach (var row in rows)
        {
            batch ??= Create(row);

            if (count >= batch.Capacity)
            {
                break;
            }

            Fill(batch, row, count);

            count++;
        }

        if (batch is null)
        {
            return new ExecutionBatch(BatchSize.MaxRowCount, [], new BatchDeepDataStore());
        }

        batch.SetRowCount(count);

        return batch;
    }

    public static ExecutionBatch Create(IRecord row)
    {
        var columns = row.Fields.Select(ToColumn).ToList();

        var capacity = BatchSize.GetRowCount(columns.Count);

        return new ExecutionBatch(capacity, [.. columns.Select(c => new BatchVector(c, capacity))], new BatchDeepDataStore());
    }

    public static void Fill(ExecutionBatch batch, IRecord row, int index)
    {
        for (var i = 0; i < batch.Vectors.Count && i < row.Fields.Count; i++)
        {
            var vector = batch.Vectors[i];

            vector.SetValue(index, BatchValueBuilder.FromField(vector.Column, row.Fields[i], batch.DeepDataContext));
        }
    }

    private static BatchColumn ToColumn(RecordField field)
        => new()
        {
            Name = field.ColumnStructure.ColumnName,
            DataType = field.ColumnStructure.DataType,
            Precision = field.ColumnStructure.Precision,
            Scale = field.ColumnStructure.Scale,
            DataLength = field.ColumnStructure.DataLength
        };
}
