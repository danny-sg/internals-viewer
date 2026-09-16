using InternalsViewer.Execution.Common.AccessPaths.Binding;
using InternalsViewer.Execution.RowMode.AccessPaths.Definitions;
using InternalsViewer.Execution.Common.AccessPaths.Predicates;
using InternalsViewer.Execution.Common.AccessPaths.Results;
using InternalsViewer.Execution.Common.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Common.AccessPaths.Search;
using InternalsViewer.Execution.Common.Interfaces;
using InternalsViewer.Execution.Common.Interfaces.Iterators;
using InternalsViewer.Execution.RowMode.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Execution.Common.Iterators;

namespace InternalsViewer.Execution.RowMode.Iterators.Row;

public sealed class FilterIterator(IIteratorFactory factory) : IteratorBase, IUnaryIterator
{
    public override PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public override AccessStrategy? Strategy => Input?.Strategy;

    public IIterator? Input { get; private set; }

    public long RowCount { get; private set; }

    public long PassedCount { get; private set; }

    private AccessPredicate? Predicate { get; set; }

    public override async Task OpenAsync(IteratorDefinition definition,
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var filter = definition.Expect<FilterDefinition>();

        if (filter.Residual is null or AccessPredicate.NoTranslation)
        {
            throw new ArgumentException("A filter needs a predicate that has been translated");
        }

        if (Input is not null)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        Predicate = filter.Residual;

        RowCount = 0;
        PassedCount = 0;

        Input = factory.Create(filter.Source);

        await Input.OpenAsync(filter.Source, context, cancellationToken);
    }

    public override async ValueTask<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Input is null)
        {
            return null;
        }

        while (await Input.GetRowAsync(cancellationToken) is { } row)
        {
            RowCount++;

            var outcome = Evaluate(row) switch
            {
                true 
                    => RowOutcome.Match,
                false 
                    => RowOutcome.NoMatch,
                _ => RowOutcome.Unknown
            };

            if (outcome == RowOutcome.Match)
            {
                PassedCount++;
            }

            var step = new AccessStep.FilterRow(RowCount, outcome)
            {
                EmittedRecord = outcome == RowOutcome.Match ? row : null,
                PassedCount = PassedCount
            };

            await EmitAsync(step, cancellationToken);

            if (outcome != RowOutcome.Match)
            {
                continue;
            }

            CurrentRow = ProjectedRecord.Project(row, OutputList);

            return CurrentRow;
        }

        CurrentRow = null;

        await EmitAsync(new AccessStep.Stopped(Input.StopReason ?? InternalsViewer.Execution.Common.AccessPaths.Results.StopReason.PageExhausted),
                        cancellationToken);

        return null;
    }

    public override async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        await base.CloseAsync();
    }

    private bool? Evaluate(IRecord row)
    {
        return PredicateEvaluator.Evaluate(Predicate!, new RecordRowValueSource(row), Context.EvaluationContext);
    }
}
