using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.Interfaces.Services.Joins;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Execution.Services.Joins.Definitions;
using InternalsViewer.Execution.Services.Joins.Inputs;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// Drives a nested loops join by pumping an outer access path and restarting an inner input for each outer row
/// </summary>
public sealed class NestedLoopsStepService(IndexStepService outerService, IndexStepService innerService) : JoinStepService
{
    public override PageAddress? CurrentPageAddress
        => IsInnerActive ? Inner.Service.CurrentPageAddress : Outer.Service.CurrentPageAddress;

    public int RebindCount { get; private set; }

    private RebindableInput InnerInput { get; set; } = null!;

    private IndexScan OuterInput => (IndexScan)Outer;

    private IRecord? CurrentOuterRecord { get; set; }

    private Queue<AccessStep> PendingEmits { get; } = new();

    private DatabaseSource Database { get; set; } = null!;

    private bool IsInnerActive { get; set; }

    private bool PendingStart { get; set; }

    private IRecord? PendingOuterRecord { get; set; }

    private AccessCounters OuterCounters { get; set; }

    private AccessCounters CompletedInnerCounters { get; set; }

    public Task StartAsync(DatabaseSource database,
                           ScanDefinition outerInput,
                           SeekDefinition innerInput,
                           CancellationToken cancellationToken,
                           EvaluationContext? evaluationContext = null,
                           JoinType joinType = JoinType.Inner)
        => StartAsync(database,
                      outerInput,
                      new CorrelatedSeek(innerService, innerInput, evaluationContext),
                      cancellationToken,
                      evaluationContext,
                      joinType);

    public async Task StartAsync(DatabaseSource database,
                                 ScanDefinition outerInput,
                                 RebindableInput inner,
                                 CancellationToken cancellationToken,
                                 EvaluationContext? evaluationContext = null,
                                 JoinType joinType = JoinType.Inner)
    {
        Database = database;
        Outer = new IndexScan(outerService, outerInput);
        Inner = inner;
        InnerInput = inner;

        ResetJoin(joinType);

        PendingEmits.Clear();

        IsInnerActive = false;
        PendingStart = true;
        PendingOuterRecord = null;
        OuterCounters = default;
        CompletedInnerCounters = default;
        RebindCount = 0;
        CurrentOuterRecord = null;

        await OuterInput.StartAsync(database, cancellationToken, evaluationContext);
    }

    public override async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
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
                    Inner.Hold(innerRecord, JoinRowState.Matched);
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

        var step = await outerService.StepNextAsync(cancellationToken);

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

            Outer.Clear();
            Outer.Hold(emitted, JoinRowState.Pending);

            Inner.Clear();
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

        var rebind = await InnerInput.RebindAsync(Database, record, RebindCount, cancellationToken);

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

        var innerRows = Inner.Buffer;

        var hasInner = innerRows.Count > 0;

        Outer.MarkState(outerRecord, hasInner ? JoinRowState.Matched : JoinRowState.Finished);

        var emits = new List<AccessStep>();

        if (hasInner && JoinType.EmitsPairs())
        {
            foreach (var inner in innerRows)
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
        if (!InnerInput.FetchesDirectly || emits.Count == 0)
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
}
