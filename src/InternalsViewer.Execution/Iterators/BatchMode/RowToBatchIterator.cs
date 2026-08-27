using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.BatchMode;

public sealed class RowToBatchIterator(IIteratorFactory factory) : IBatchIterator
{
    public int NodeId { get; private set; }

    public bool IsComplete { get; private set; }

    public StopReason? StopReason { get; private set; }

    public long BatchCount { get; private set; }

    private IteratorContext Context { get; set; } = null!;

    private IIterator? Input { get; set; }

    private ExecutionBatch? Batch { get; set; }

    public async Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
    {
        var adapter = definition.Expect<RowToBatchDefinition>();

        Context = context;

        NodeId = definition.NodeId;

        BatchCount = 0;

        IsComplete = false;

        StopReason = null;

        Batch = null;

        await EmitAsync(new AccessStep.Open(), cancellationToken);

        Input = factory.Create(adapter.Row);

        await Input.OpenAsync(adapter.Row, context, cancellationToken);
    }

    public async Task<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        Batch?.DeepDataContext.Clear();

        var count = 0;

        while (count < (Batch?.Capacity ?? BatchSize.MaxRowCount) && await Input.GetRowAsync(cancellationToken) is { } row)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Batch ??= CreateBatch(row);

            Pack(Batch, row, count);

            count++;
        }

        if (count == 0 || Batch is null)
        {
            IsComplete = true;

            StopReason = Input.StopReason ?? AccessPaths.Results.StopReason.PageExhausted;

            return null;
        }

        Batch.SetRowCount(count);

        BatchCount++;

        return Batch;
    }

    public async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        Batch = null;

        IsComplete = true;

        await EmitAsync(new AccessStep.Close(), CancellationToken.None);
    }

    private ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        => Context.Steps.EmitAsync(step with { NodeId = NodeId }, cancellationToken);

    private static ExecutionBatch CreateBatch(IRecord row)
    {
        var columns = row.Fields.Select(ToColumn).ToList();

        var capacity = BatchSize.GetRowCount(columns.Count);

        return new ExecutionBatch(capacity, [.. columns.Select(c => new BatchVector(c, capacity))], new BatchDeepDataStore());
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

    private static void Pack(ExecutionBatch batch, IRecord row, int index)
    {
        for (var i = 0; i < batch.Vectors.Count && i < row.Fields.Count; i++)
        {
            var vector = batch.Vectors[i];

            vector.Slots[index] = BatchSlotBuilder.FromField(vector.Column, row.Fields[i], batch.DeepDataContext);
        }
    }
}
