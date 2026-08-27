using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.BatchMode;

namespace InternalsViewer.Execution.Iterators.BatchMode;

public sealed class BatchComputeScalarIterator(IIteratorFactory factory) : IBatchIterator
{
    public int NodeId { get; private set; }

    public bool IsComplete { get; private set; }

    public StopReason? StopReason { get; private set; }

    public long BatchCount { get; private set; }

    public IReadOnlyList<ComputedColumn> Columns { get; private set; } = [];

    private IteratorContext Context { get; set; } = null!;

    private IBatchIterator? Input { get; set; }

    private BatchRowValueSource Values { get; } = new();

    public async Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
    {
        var compute = definition.Expect<BatchComputeScalarDefinition>();

        Context = context;

        NodeId = definition.NodeId;

        Columns = compute.Columns;

        BatchCount = 0;

        IsComplete = false;

        StopReason = null;

        await EmitAsync(new AccessStep.Open(), cancellationToken);

        Input = factory.CreateBatch(compute.Source);

        await Input.OpenAsync(compute.Source, context, cancellationToken);
    }

    public async Task<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        if (await Input.GetNextBatchAsync(cancellationToken) is not { } batch)
        {
            IsComplete = true;

            StopReason = Input.StopReason ?? AccessPaths.Results.StopReason.RowGroupsExhausted;

            await EmitAsync(new AccessStep.Stopped(StopReason.Value), cancellationToken);

            return null;
        }

        BatchCount++;

        var computed = Compute(batch);

        var columnNames = string.Join(", ", Columns.Select(c => c.Name));

        await EmitAsync(new AccessStep.ComputeVector(BatchCount, batch.RowGroupId, columnNames, computed), cancellationToken);

        return batch;
    }

    public async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        IsComplete = true;

        await EmitAsync(new AccessStep.Close(), CancellationToken.None);
    }

    private ValueTask EmitAsync(AccessStep step, CancellationToken cancellationToken)
        => Context.Steps.EmitAsync(step with { NodeId = NodeId }, cancellationToken);

    private int Compute(ExecutionBatch batch)
    {
        if (Columns.Count == 0)
        {
            return 0;
        }

        var vectors = Bind(batch);

        Values.Bind(batch);

        var selection = batch.SelectionVector;

        for (var i = 0; i < selection.RowCount; i++)
        {
            var row = selection[i];

            Values.MoveTo(row);

            for (var c = 0; c < Columns.Count; c++)
            {
                var value = PredicateEvaluator.Resolve(Columns[c].Expression, Values, Context.EvaluationContext);

                if (Columns[c].DataType is { } dataType)
                {
                    value = AccessValueConverter.ConvertTo(value, dataType);
                }

                var column = vectors[c].Column;

                if (column.DataType == SqlDbType.Variant && !value.IsNull)
                {
                    column.DataType = value.DataType;
                }

                vectors[c].Slots[row] = BatchSlotBuilder.FromValue(column, value, batch.DeepDataContext);
            }
        }

        return selection.RowCount;
    }

    private List<BatchVector> Bind(ExecutionBatch batch)
    {
        var vectors = new List<BatchVector>(Columns.Count);

        foreach (var column in Columns)
        {
            if (batch.FindVector(column.Name) is { } existing)
            {
                vectors.Add(existing);

                continue;
            }

            var added = new BatchVector(new BatchColumn
                                        {
                                            Name = column.Name,
                                            DataType = column.DataType ?? SqlDbType.Variant
                                        },
                                        batch.Capacity);

            batch.AddVector(added);

            vectors.Add(added);
        }

        return vectors;
    }
}
