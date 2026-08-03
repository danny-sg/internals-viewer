using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Execution.Services.Joins.Inputs;

/// <summary>
/// An input that seeks an index for key values taken from the outer row, as a key lookup does
/// </summary>
public sealed class CorrelatedSeekJoinInput(IndexStepService service,
                                            SeekDefinition input,
                                            EvaluationContext? evaluationContext = null) : RebindableJoinInput
{
    public override IStepService Service => service;

    public override AccessStrategy? Strategy => service.Strategy;

    public override bool FetchesDirectly => false;

    public override async Task<AccessStep> RebindAsync(DatabaseSource database,
                                                       IRecord outerRecord,
                                                       int rebindNumber,
                                                       CancellationToken cancellationToken)
    {
        GuardResidual(input.Residual, outerRecord, InnerColumns(database));

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

    private IReadOnlySet<string> InnerColumns(DatabaseSource database)
    {
        var structure = IndexStructureProvider.GetIndexStructure(database, input.AllocationUnitId);

        var names = structure.Columns.Select(c => c.ColumnName);

        if (structure.TableStructure is { } table)
        {
            names = names.Concat(table.Columns.Select(c => c.ColumnName));
        }

        return names.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
