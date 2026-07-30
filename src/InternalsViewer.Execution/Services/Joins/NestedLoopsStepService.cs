using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Services.Indexes;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// Drives a nested loops join by pumping an outer access path and re-seeking an inner path with correlated key values for each outer row
/// </summary>
public sealed class NestedLoopsStepService(IndexStepService outerService, IndexStepService innerService) : IJoinStepService
{
    public const int OuterSource = 0;

    public const int InnerSource = 1;

    public const int JoinSource = -1;

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; private set; }

    public PageAddress? CurrentPageAddress => IsInnerActive ? InnerService.CurrentPageAddress : OuterService.CurrentPageAddress;

    public AccessStrategy? Strategy => OuterService.Strategy;

    public AccessStrategy? OuterStrategy => OuterService.Strategy;

    public AccessStrategy? InnerStrategy { get; private set; }

    public int RebindCount { get; private set; }

    private IndexStepService OuterService { get; } = outerService;

    private IndexStepService InnerService { get; } = innerService;

    private DatabaseSource Database { get; set; } = null!;

    private NestedLoopsInnerInput InnerInput { get; set; } = null!;

    private EvaluationContext? EvaluationContext { get; set; }

    private bool IsInnerActive { get; set; }

    private bool PendingStart { get; set; }

    private IRecord? PendingOuterRecord { get; set; }

    private AccessCounters OuterCounters { get; set; }

    private AccessCounters CompletedInnerCounters { get; set; }

    private List<AccessStep> TakenSteps { get; } = [];

    public async Task StartAsync(DatabaseSource database,
                                 NestedLoopsOuterInput outerInput,
                                 NestedLoopsInnerInput innerInput,
                                 CancellationToken cancellationToken,
                                 EvaluationContext? evaluationContext = null)
    {
        Database = database;
        InnerInput = innerInput;
        EvaluationContext = evaluationContext;

        IsInnerActive = false;
        PendingStart = true;
        PendingOuterRecord = null;
        OuterCounters = default;
        CompletedInnerCounters = default;
        RebindCount = 0;
        InnerStrategy = null;
        IsComplete = false;

        TakenSteps.Clear();

        await OuterService.StartAsync(database,
                                      outerInput.AllocationUnitId,
                                      outerInput.RootPage,
                                      outerInput.Ranges,
                                      outerInput.Residual,
                                      outerInput.Direction,
                                      cancellationToken,
                                      outerInput.RowGoal,
                                      outerInput.HasUntranslatedResidual,
                                      evaluationContext);
    }

    public async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        if (PendingStart)
        {
            PendingStart = false;

            var bindings = string.Join(", ", InnerInput.Bindings.Select(b => $"{b.SeekColumn} = {b.OuterColumn}"));

            var start = new AccessStep.JoinStart($"Nested loops — each outer row binds {bindings} for the inner seek")
            {
                Source = JoinSource
            };

            TakenSteps.Add(start);

            return start;
        }

        if (PendingOuterRecord is { } record)
        {
            PendingOuterRecord = null;

            return await RebindAsync(record, cancellationToken);
        }

        if (IsInnerActive)
        {
            var innerStep = await InnerService.StepNextAsync(cancellationToken);

            if (innerStep is not (null or AccessStep.Stopped))
            {
                return Take(innerStep, InnerSource, OuterCounters.Add(CompletedInnerCounters).Add(innerStep.Counters));
            }

            var finalCounters = innerStep?.Counters ?? InnerService.Current?.Counters ?? default;

            CompletedInnerCounters = CompletedInnerCounters.Add(finalCounters);

            IsInnerActive = false;
        }

        var step = await OuterService.StepNextAsync(cancellationToken);

        if (step is null)
        {
            IsComplete = true;

            return null;
        }

        OuterCounters = step.Counters;

        if (step is AccessStep.Row { EmittedRecord: { } emitted })
        {
            PendingOuterRecord = emitted;
        }

        if (step is AccessStep.Stopped)
        {
            IsComplete = true;
        }

        return Take(step, OuterSource, OuterCounters.Add(CompletedInnerCounters));
    }

    private async Task<AccessStep> RebindAsync(IRecord record, CancellationToken cancellationToken)
    {
        var source = new RecordRowValueSource(record);

        var values = new AccessValue[InnerInput.Bindings.Count];

        for (var index = 0; index < InnerInput.Bindings.Count; index++)
        {
            var binding = InnerInput.Bindings[index];

            if (!record.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName, binding.OuterColumn, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Outer row has no column '{binding.OuterColumn}' to bind seek column '{binding.SeekColumn}'");
            }

            values[index] = source.GetValue(-1, binding.OuterColumn).WithColumnName(binding.SeekColumn);
        }

        var key = new AccessKey([.. values]);

        RebindCount++;

        await InnerService.StartAsync(Database,
                                      InnerInput.AllocationUnitId,
                                      InnerInput.RootPage,
                                      [SeekBounds.Equality(key)],
                                      InnerInput.Residual,
                                      ScanDirection.Forward,
                                      cancellationToken,
                                      InnerInput.RowGoal,
                                      evaluationContext: EvaluationContext);

        InnerStrategy ??= InnerService.Strategy;

        IsInnerActive = true;

        var rebind = new AccessStep.Rebind(RebindCount, key)
        {
            Source = InnerSource,
            Counters = OuterCounters.Add(CompletedInnerCounters)
        };

        TakenSteps.Add(rebind);

        return rebind;
    }

    private AccessStep Take(AccessStep step, int source, AccessCounters counters)
    {
        var taken = step with { Source = source, Counters = counters };

        TakenSteps.Add(taken);

        return taken;
    }
}
