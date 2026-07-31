using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

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

    public int PairCount { get; private set; }

    public JoinType JoinType { get; private set; } = JoinType.Inner;

    public IReadOnlyList<JoinBufferRow> OuterBuffer
        => CurrentOuterRecord is { } record ? [new JoinBufferRow(record, OuterRowState)] : [];

    /// <summary>
    /// Rows the inner side has returned for the current rebind
    /// </summary>
    /// <remarks>
    /// Every row the inner seek returns satisfies the bound key, so each one is a match for the outer row that bound it.
    /// </remarks>
    public IReadOnlyList<JoinBufferRow> InnerBuffer => InnerRecords;

    private IRecord? CurrentOuterRecord { get; set; }

    private JoinRowState OuterRowState { get; set; }

    private List<JoinBufferRow> InnerRecords { get; } = [];

    private Queue<AccessStep> PendingEmits { get; } = new();

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
                                 EvaluationContext? evaluationContext = null,
                                 JoinType joinType = JoinType.Inner)
    {
        Database = database;
        InnerInput = innerInput;
        EvaluationContext = evaluationContext;
        JoinType = joinType;
        PairCount = 0;
        PendingEmits.Clear();

        IsInnerActive = false;
        PendingStart = true;
        PendingOuterRecord = null;
        OuterCounters = default;
        CompletedInnerCounters = default;
        RebindCount = 0;
        InnerStrategy = null;
        IsComplete = false;
        CurrentOuterRecord = null;

        InnerRecords.Clear();
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

            var start = new AccessStep.JoinStart($"{JoinType.ToDisplayName()} on {bindings}. Each outer row binds the inner seek")
            {
                Source = JoinSource
            };

            TakenSteps.Add(start);

            return start;
        }

        if (PendingEmits.Count > 0)
        {
            return TakePendingEmit();
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
                if (innerStep is AccessStep.Row { EmittedRecord: { } innerRecord })
                {
                    InnerRecords.Add(new JoinBufferRow(innerRecord, JoinRowState.Matched));
                }

                return Take(innerStep, InnerSource, OuterCounters.Add(CompletedInnerCounters).Add(innerStep.Counters));
            }

            var finalCounters = innerStep?.Counters ?? InnerService.Current?.Counters ?? default;

            CompletedInnerCounters = CompletedInnerCounters.Add(finalCounters);

            IsInnerActive = false;

            QueueRebindEmits();

            if (PendingEmits.Count > 0)
            {
                return TakePendingEmit();
            }
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
            CurrentOuterRecord = emitted;
            OuterRowState = JoinRowState.Pending;

            InnerRecords.Clear();
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

    /// <summary>
    /// Works out what the rebind that just finished contributes to the output
    /// </summary>
    /// <remarks>
    /// A rebind that returned nothing is the loop join's equivalent of a comparison that finds no partner, so it is the point the join
    /// type decides whether the outer row is dropped or preserved.
    /// </remarks>
    private void QueueRebindEmits()
    {
        if (CurrentOuterRecord is not { } outerRecord)
        {
            return;
        }

        var counters = OuterCounters.Add(CompletedInnerCounters);

        OuterRowState = InnerRecords.Count > 0 ? JoinRowState.Matched : JoinRowState.Finished;

        PendingEmits.Enqueue(new AccessStep.JoinVerdict(JoinType.Decide(true, InnerRecords.Count > 0))
        {
            Source = JoinSource,
            Counters = counters
        });

        if (InnerRecords.Count > 0)
        {
            if (JoinType.EmitsPairs())
            {
                foreach (var inner in InnerRecords)
                {
                    PairCount++;

                    PendingEmits.Enqueue(new AccessStep.JoinEmit(PairCount)
                    {
                        OuterRecord = outerRecord,
                        InnerRecord = inner.Record,
                        Source = JoinSource,
                        Counters = counters
                    });
                }
            }
            else if (JoinType.EmitsOuterOnMatch())
            {
                PairCount++;

                PendingEmits.Enqueue(new AccessStep.JoinEmit(PairCount)
                {
                    OuterRecord = outerRecord,
                    Source = JoinSource,
                    Counters = counters
                });
            }

            return;
        }

        if (JoinType.PreservesOuter())
        {
            PairCount++;

            PendingEmits.Enqueue(new AccessStep.JoinEmit(PairCount)
            {
                OuterRecord = outerRecord,
                IsUnmatched = true,
                Source = JoinSource,
                Counters = counters
            });
        }
    }

    private AccessStep TakePendingEmit()
    {
        var step = PendingEmits.Dequeue();

        TakenSteps.Add(step);

        return step;
    }

    private AccessStep Take(AccessStep step, int source, AccessCounters counters)
    {
        var taken = step with { Source = source, Counters = counters };

        TakenSteps.Add(taken);

        return taken;
    }
}
