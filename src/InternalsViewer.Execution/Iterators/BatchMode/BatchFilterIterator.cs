using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.BatchMode;

namespace InternalsViewer.Execution.Iterators.BatchMode;

public sealed class BatchFilterIterator(IIteratorFactory factory) : IBatchIterator
{
    public int NodeId { get; private set; }

    public bool IsComplete { get; private set; }

    public StopReason? StopReason { get; private set; }

    public long BatchCount { get; private set; }

    public long RowCount { get; private set; }

    public long PassedCount { get; private set; }

    public ExecutionBatch? CurrentBatch => Input?.CurrentBatch;

    public IReadOnlyList<BatchVector> OutputVectors => Input?.OutputVectors ?? [];

    public long BatchNumber => Input?.BatchNumber ?? 0;

    public IBatchIterator? Input { get; private set; }

    private IteratorContext Context { get; set; } = null!;

    private AccessPredicate? Predicate { get; set; }

    private string Columns { get; set; } = string.Empty;

    private RowOutcome[] Outcomes { get; set; } = [];

    private BatchRowValueSource Values { get; } = new();

    public async Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
    {
        var filter = definition.Expect<BatchFilterDefinition>();

        if (filter.Residual is null or AccessPredicate.NoTranslation)
        {
            throw new ArgumentException("A filter needs a predicate that has been translated");
        }

        Context = context;

        NodeId = definition.NodeId;

        Predicate = filter.Residual;

        Columns = string.Join(", ", PredicateColumns.Referenced(filter.Residual).Distinct());

        BatchCount = 0;

        RowCount = 0;

        PassedCount = 0;

        IsComplete = false;

        StopReason = null;

        await EmitAsync(new AccessStep.Open(), cancellationToken);

        Input = factory.CreateBatch(filter.Source);

        await Input.OpenAsync(filter.Source, context, cancellationToken);
    }

    public async Task<ExecutionBatch?> GetNextBatchAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        while (await Input.GetNextBatchAsync(cancellationToken) is { } batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BatchCount++;

            var selection = batch.SelectionVector;

            var read = selection.RowCount;

            var matches = Evaluate(batch, selection, read);

            RowCount += read;

            await EmitAsync(new AccessStep.FilterVector(BatchCount, batch.RowGroupId, Columns, read, matches),
                            cancellationToken);

            Compact(selection, read);

            PassedCount += selection.RowCount;

            await EmitAsync(new AccessStep.BatchFiltered(BatchCount,
                                                         batch.RowGroupId,
                                                         read,
                                                         selection.RowCount,
                                                         PassedCount),
                            cancellationToken);

            if (selection.RowCount == 0)
            {
                continue;
            }

            return batch;
        }

        IsComplete = true;

        StopReason = Input.StopReason ?? AccessPaths.Results.StopReason.RowGroupsExhausted;

        await EmitAsync(new AccessStep.Stopped(StopReason.Value), cancellationToken);

        return null;
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

    private int Evaluate(ExecutionBatch batch, SelectionVector selection, int read)
    {
        if (Outcomes.Length < batch.Capacity)
        {
            Outcomes = new RowOutcome[batch.Capacity];
        }

        Values.Bind(batch);

        var matches = 0;

        for (var i = 0; i < read; i++)
        {
            var row = selection[i];

            Values.MoveTo(row);

            var outcome = Evaluate() switch
            {
                true 
                    => RowOutcome.Match,
                false 
                    => RowOutcome.NoMatch,
                _ => RowOutcome.Unknown
            };

            Outcomes[row] = outcome;

            if (outcome == RowOutcome.Match)
            {
                matches++;
            }
        }

        return matches;
    }

    private void Compact(SelectionVector selection, int read)
    {
        selection.RemoveAll();

        for (var i = 0; i < read; i++)
        {
            var row = selection[i];

            if (Outcomes[row] == RowOutcome.Match)
            {
                selection.Add(row);
            }
        }
    }

    private bool? Evaluate()
        => PredicateEvaluator.Evaluate(Predicate!, Values, Context.EvaluationContext);
}
