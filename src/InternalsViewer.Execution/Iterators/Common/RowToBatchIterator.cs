using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.BatchMode;

namespace InternalsViewer.Execution.Iterators.Common;

public sealed class RowToBatchIterator(IIteratorFactory factory) : IBatchIterator
{
    public int NodeId { get; private set; }

    public bool IsComplete { get; private set; }

    public StopReason? StopReason { get; private set; }

    public long BatchCount { get; private set; }

    public ExecutionBatch? CurrentBatch => Batch;

    public IReadOnlyList<BatchVector> OutputVectors => Batch?.Vectors ?? [];

    public IBatchIterator? Input => null;

    public IIterator? Source { get; private set; }

    private IteratorContext Context { get; set; } = null!;

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

        Source = factory.Create(adapter.Row);

        await Source.OpenAsync(adapter.Row, context, cancellationToken);
    }

    public async Task<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Source is null)
        {
            return null;
        }

        Batch?.DeepDataContext.Clear();

        var count = 0;

        while (count < (Batch?.Capacity ?? BatchSize.MaxRowCount) && await Source.GetRowAsync(cancellationToken) is { } row)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Batch ??= BatchPacker.Create(row);

            BatchPacker.Fill(Batch, row, count);

            count++;
        }

        if (count == 0 || Batch is null)
        {
            IsComplete = true;

            StopReason = Source.StopReason ?? AccessPaths.Results.StopReason.PageExhausted;

            return null;
        }

        Batch.SetRowCount(count);

        BatchCount++;

        return Batch;
    }

    public async Task CloseAsync()
    {
        if (Source is not null)
        {
            await Source.CloseAsync();
        }

        Batch = null;

        IsComplete = true;

        await EmitAsync(new AccessStep.Close(), CancellationToken.None);
    }

    private ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        => Context.Steps.EmitAsync(step with { NodeId = NodeId }, cancellationToken);

}
