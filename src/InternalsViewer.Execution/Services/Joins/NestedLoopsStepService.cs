using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Joins;
using InternalsViewer.Execution.AccessPaths.Search;
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

    public PageAddress? CurrentPageAddress => IsInnerActive ? Inner.Service.CurrentPageAddress : OuterService.CurrentPageAddress;

    public AccessStrategy? Strategy => OuterService.Strategy;

    public AccessStrategy? OuterStrategy => OuterService.Strategy;

    public AccessStrategy? InnerStrategy => Inner.Strategy;

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

    private ILoopInnerSide Inner { get; set; } = null!;

    private DatabaseSource Database { get; set; } = null!;

    private bool IsInnerActive { get; set; }

    private bool PendingStart { get; set; }

    private IRecord? PendingOuterRecord { get; set; }

    private AccessCounters OuterCounters { get; set; }

    private AccessCounters CompletedInnerCounters { get; set; }

    private List<AccessStep> TakenSteps { get; } = [];

    public Task StartAsync(DatabaseSource database,
                           NestedLoopsOuterInput outerInput,
                           NestedLoopsInnerInput innerInput,
                           CancellationToken cancellationToken,
                           EvaluationContext? evaluationContext = null,
                           JoinType joinType = JoinType.Inner)
        => StartAsync(database,
                      outerInput,
                      new CorrelatedSeekInnerSide(innerService, innerInput, evaluationContext),
                      cancellationToken,
                      evaluationContext,
                      joinType);

    public async Task StartAsync(DatabaseSource database,
                                 NestedLoopsOuterInput outerInput,
                                 ILoopInnerSide inner,
                                 CancellationToken cancellationToken,
                                 EvaluationContext? evaluationContext = null,
                                 JoinType joinType = JoinType.Inner)
    {
        Database = database;
        Inner = inner;
        JoinType = joinType;
        PairCount = 0;
        PendingEmits.Clear();

        IsInnerActive = false;
        PendingStart = true;
        PendingOuterRecord = null;
        OuterCounters = default;
        CompletedInnerCounters = default;
        RebindCount = 0;
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

            var start = new AccessStep.JoinStart($"{JoinType.ToDisplayName()} {Inner.StartDescription}")
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
            var innerStep = await Inner.Service.StepNextAsync(cancellationToken);

            if (innerStep is not (null or AccessStep.Stopped))
            {
                if (innerStep is AccessStep.Row { EmittedRecord: { } innerRecord })
                {
                    InnerRecords.Add(new JoinBufferRow(innerRecord, JoinRowState.Matched));
                }

                return Take(innerStep, InnerSource, OuterCounters.Add(CompletedInnerCounters).Add(innerStep.Counters));
            }

            var finalCounters = innerStep?.Counters ?? Inner.Service.Current?.Counters ?? default;

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
        RebindCount++;

        var rebind = await Inner.RebindAsync(Database, record, RebindCount, cancellationToken);

        IsInnerActive = true;

        var step = rebind with
        {
            Source = InnerSource,
            Counters = OuterCounters.Add(CompletedInnerCounters)
        };

        TakenSteps.Add(step);

        return step;
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

        var hasInner = InnerRecords.Count > 0;

        OuterRowState = hasInner ? JoinRowState.Matched : JoinRowState.Finished;

        var emits = new List<AccessStep>();

        if (hasInner && JoinType.EmitsPairs())
        {
            foreach (var inner in InnerRecords)
            {
                PairCount++;

                emits.Add(new AccessStep.JoinEmit(PairCount)
                {
                    OuterRecord = outerRecord,
                    InnerRecord = inner.Record,
                    Source = JoinSource,
                    Counters = counters
                });
            }
        }
        else if (hasInner && JoinType.EmitsOuterOnMatch())
        {
            PairCount++;

            emits.Add(new AccessStep.JoinEmit(PairCount)
            {
                OuterRecord = outerRecord,
                Source = JoinSource,
                Counters = counters
            });
        }
        else if (!hasInner && JoinType.PreservesOuter())
        {
            PairCount++;

            emits.Add(new AccessStep.JoinEmit(PairCount)
            {
                OuterRecord = outerRecord,
                IsUnmatched = true,
                Source = JoinSource,
                Counters = counters
            });
        }

        // A direct fetch weighs nothing up: the row was addressed rather than searched for, so stating a verdict beside the row it
        // produced would only repeat it. The verdict is still stated when it explains why nothing came out.
        if (!Inner.FetchesDirectly || emits.Count == 0)
        {
            PendingEmits.Enqueue(new AccessStep.JoinVerdict(JoinType.Decide(true, hasInner))
            {
                Source = JoinSource,
                Counters = counters
            });
        }

        foreach (var emit in emits)
        {
            PendingEmits.Enqueue(emit);
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
