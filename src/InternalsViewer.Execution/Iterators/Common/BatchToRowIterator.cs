using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;


namespace InternalsViewer.Execution.Iterators.Common;

/// <summary>
/// Iterator that converts between Batch Mode and Row Mode
/// </summary>
/// <remarks>
/// Reads per batch, materializes the batch vector to rows, then emits each row as a Row Mode iterator.
///
/// This does exist as CQScanBatchHelper, but it's not visible on query plans
/// </remarks>
public sealed class BatchToRowIterator(IIteratorFactory factory) : IteratorBase
{
    public override PageAddress? CurrentPageAddress => null;

    public override AccessStrategy? Strategy => null;

    public IBatchIterator? Source { get; private set; }

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

        Source = factory.CreateBatch(adapter.Batch);

        await Source.OpenAsync(adapter.Batch, context, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Source is null)
        {
            return null;
        }

        while (true)
        {
            if (Batch is null || Position >= Batch.SelectionVector.RowCount)
            {
                Batch = await Source.GetNextBatchAsync(cancellationToken);

                Position = 0;

                if (Batch is null)
                {
                    CurrentRow = null;

                    IsComplete = true;

                    return null;
                }

                continue;
            }

            CurrentRow = ProjectedRecord.Project(BatchRecordBuilder.Build(Batch, Batch.SelectionVector[Position]), OutputList);

            Position++;

            return CurrentRow;
        }
    }

    public override async Task CloseAsync()
    {
        if (Source is not null)
        {
            await Source.CloseAsync();
        }

        Batch = null;

        await base.CloseAsync();
    }
}
