using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// An inner side that seeks an index for key values taken from the outer row, as a key lookup does
/// </summary>
public sealed class CorrelatedSeekInnerSide(IndexStepService service,
                                            NestedLoopsInnerInput input,
                                            EvaluationContext? evaluationContext = null) : ILoopInnerSide
{
    public IStepService Service => service;

    public AccessStrategy? Strategy => service.Strategy;

    public string StartDescription
        => $"on {string.Join(", ", input.Bindings.Select(b => $"{b.SeekColumn} = {b.OuterColumn}"))}. "
           + "Each outer row binds the inner seek";

    public bool FetchesDirectly => false;

    public async Task<AccessStep> RebindAsync(DatabaseSource database,
                                              IRecord outerRecord,
                                              int rebindNumber,
                                              CancellationToken cancellationToken)
    {
        var source = new RecordRowValueSource(outerRecord);

        var values = new AccessValue[input.Bindings.Count];

        for (var index = 0; index < input.Bindings.Count; index++)
        {
            var binding = input.Bindings[index];

            if (!outerRecord.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName,
                                                           binding.OuterColumn,
                                                           StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Outer row has no column '{binding.OuterColumn}' "
                                                    + $"to bind seek column '{binding.SeekColumn}'");
            }

            values[index] = source.GetValue(-1, binding.OuterColumn).WithColumnName(binding.SeekColumn);
        }

        var key = new AccessKey([.. values]);

        await service.StartAsync(database,
                                 input.AllocationUnitId,
                                 input.RootPage,
                                 [SeekBounds.Equality(key)],
                                 input.Residual,
                                 ScanDirection.Forward,
                                 cancellationToken,
                                 input.RowGoal,
                                 evaluationContext: evaluationContext);

        return new AccessStep.Rebind(rebindNumber, key);
    }
}
