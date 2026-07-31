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
/// Drives a merge join by walking two ordered access paths in lockstep, advancing whichever side has the lower key and emitting row
/// pairs on a match
/// </summary>
/// <remarks>
/// Duplicate join keys are handled by buffering the inner group in memory, standing in for the worktable a many-to-many merge join uses,
/// so replayed pairs are emitted without re-reading pages.
/// </remarks>
public sealed class MergeJoinStepService(IndexStepService outerService, IndexStepService innerService) : IJoinStepService
{
    public const int OuterSource = 0;

    public const int InnerSource = 1;

    public const int JoinSource = -1;

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; private set; }

    public PageAddress? CurrentPageAddress
        => Current?.Source == InnerSource ? InnerService.CurrentPageAddress : OuterService.CurrentPageAddress;

    public AccessStrategy? Strategy => OuterService.Strategy;

    public AccessStrategy? OuterStrategy => OuterService.Strategy;

    public AccessStrategy? InnerStrategy => InnerService.Strategy;

    public int PairCount { get; private set; }

    public JoinType JoinType { get; private set; } = JoinType.Inner;

    /// <summary>
    /// Rows the outer side has returned since the last pairing was made
    /// </summary>
    /// <remarks>
    /// Rows build up as each side is advanced, showing the work that went into the next pairing. Once a pairing completes only the rows
    /// still in play are carried over, which is the row each walk has already read ahead of the matched key.
    /// </remarks>
    public IReadOnlyList<JoinBufferRow> OuterBuffer => OuterRows;

    /// <summary>
    /// Rows the inner side has returned since the last pairing was made
    /// </summary>
    public IReadOnlyList<JoinBufferRow> InnerBuffer => InnerRows;

    private SideCursor? Outer { get; set; }

    private SideCursor? Inner { get; set; }

    private List<IRecord> Group { get; } = [];

    private List<JoinBufferRow> OuterRows { get; } = [];

    private List<JoinBufferRow> InnerRows { get; } = [];

    

    private IndexStepService OuterService { get; } = outerService;

    private IndexStepService InnerService { get; } = innerService;

    private IReadOnlyList<string> OuterColumns { get; set; } = [];

    private IReadOnlyList<string> InnerColumns { get; set; } = [];

    private int CompareWidth { get; set; }

    private int ComparisonSign { get; set; } = 1;

    private AccessCounters OuterCounters { get; set; }

    private AccessCounters InnerCounters { get; set; }

    private CancellationToken CurrentToken { get; set; }

    private IAsyncEnumerator<AccessStep>? Steps { get; set; }

    private List<AccessStep> TakenSteps { get; } = [];

    public async Task StartAsync(DatabaseSource database,
                                 MergeJoinSideInput outerInput,
                                 MergeJoinSideInput innerInput,
                                 CancellationToken cancellationToken,
                                 EvaluationContext? evaluationContext = null,
                                 JoinType joinType = JoinType.Inner)
    {
        JoinType = joinType;
        OuterColumns = outerInput.JoinColumns;
        InnerColumns = innerInput.JoinColumns;
        CompareWidth = Math.Min(OuterColumns.Count, InnerColumns.Count);
        ComparisonSign = outerInput.Direction == ScanDirection.Backward ? -1 : 1;

        OuterCounters = default;
        InnerCounters = default;
        PairCount = 0;
        IsComplete = false;

        Outer = null;
        Inner = null;

        Group.Clear();
        OuterRows.Clear();
        InnerRows.Clear();
        TakenSteps.Clear();

        if (Steps is not null)
        {
            await Steps.DisposeAsync();
        }

        await OuterService.StartAsync(database,
                                      outerInput.AllocationUnitId,
                                      outerInput.RootPage,
                                      outerInput.Ranges,
                                      outerInput.Residual,
                                      outerInput.Direction,
                                      cancellationToken,
                                      hasUntranslatedResidual: outerInput.HasUntranslatedResidual,
                                      evaluationContext: evaluationContext);

        await InnerService.StartAsync(database,
                                      innerInput.AllocationUnitId,
                                      innerInput.RootPage,
                                      innerInput.Ranges,
                                      innerInput.Residual,
                                      innerInput.Direction,
                                      cancellationToken,
                                      hasUntranslatedResidual: innerInput.HasUntranslatedResidual,
                                      evaluationContext: evaluationContext);

        Steps = Run().GetAsyncEnumerator(CancellationToken.None);
    }

    public async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Steps is null)
        {
            return null;
        }

        CurrentToken = cancellationToken;

        if (!await Steps.MoveNextAsync())
        {
            IsComplete = true;

            return null;
        }

        var step = Steps.Current;

        TakenSteps.Add(step);

        if (step is AccessStep.Stopped)
        {
            IsComplete = true;
        }

        return step;
    }

    private async IAsyncEnumerable<AccessStep> Run()
    {
        var outer = Outer = new SideCursor(OuterService, OuterSource, this);

        var inner = Inner = new SideCursor(InnerService, InnerSource, this);

        yield return Stamp(new AccessStep.JoinStart($"Reading Outer"), JoinSource);

        await foreach (var step in outer.AdvanceAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }

        yield return Stamp(new AccessStep.JoinStart("Reading Inner"), JoinSource);

        await foreach (var step in inner.AdvanceAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }

        while (outer.CurrentRecord is { } outerRecord && inner.CurrentRecord is { } innerRecord)
        {
            var outerKey = GetKey(outerRecord, OuterColumns);

            var innerKey = GetKey(innerRecord, InnerColumns);

            // A null key never equals anything, so the row is unmatched whatever the other side holds
            var comparison = HasNull(outerKey) ? -1
                : HasNull(innerKey) ? 1
                : outerKey.ComparePrefix(innerKey, CompareWidth) * ComparisonSign;

            if (comparison < 0)
            {
                var action = "Outer < Inner";

                MarkState(OuterSource, outerRecord, JoinRowState.Finished);

                yield return Stamp(Compare(outerKey, innerKey, comparison, action), JoinSource);

                if (JoinType.PreservesOuter())
                {
                    yield return Stamp(Unmatched(outerRecord, null), JoinSource);
                }

                await foreach (var step in outer.AdvanceAsync().WithCancellation(CurrentToken))
                {
                    yield return step;
                }

                continue;
            }

            if (comparison > 0)
            {
                var action = "Inner < Outer";

                MarkState(InnerSource, innerRecord, JoinRowState.Finished);

                yield return Stamp(Compare(outerKey, innerKey, comparison, action), JoinSource);

                if (JoinType.PreservesInner())
                {
                    yield return Stamp(Unmatched(null, innerRecord), JoinSource);
                }

                await foreach (var step in inner.AdvanceAsync().WithCancellation(CurrentToken))
                {
                    yield return step;
                }

                continue;
            }

            MarkMatched(OuterSource, outerRecord);
            MarkMatched(InnerSource, innerRecord);

            yield return Stamp(Compare(outerKey, innerKey, comparison, "Outer = Inner"), JoinSource);

            var group = Group;

            group.Clear();

            var groupKey = innerKey;

            while (inner.CurrentRecord is { } groupRecord && GetKey(groupRecord, InnerColumns).ComparePrefix(groupKey, CompareWidth) == 0)
            {
                group.Add(groupRecord);

                MarkMatched(InnerSource, groupRecord);

                // The advance that ends the group has read a row for the next comparison, which is held back until it can be marked as such
                var steps = new List<AccessStep>();

                await foreach (var step in inner.AdvanceAsync().WithCancellation(CurrentToken))
                {
                    steps.Add(step);
                }

                var isReadAhead = inner.CurrentRecord is not { } next
                                  || GetKey(next, InnerColumns).ComparePrefix(groupKey, CompareWidth) != 0;

                foreach (var step in steps)
                {
                    yield return isReadAhead && step is AccessStep.Row row ? row with { IsReadAhead = true } : step;
                }
            }

            var isFirstOuter = true;

            while (outer.CurrentRecord is { } matchRecord && GetKey(matchRecord, OuterColumns).ComparePrefix(groupKey, CompareWidth) == 0)
            {
                MarkMatched(OuterSource, matchRecord);

                if (JoinType.EmitsPairs())
                {
                    foreach (var groupRecord in group)
                    {
                        PairCount++;

                        yield return Stamp(new AccessStep.JoinEmit(PairCount)
                                           {
                                               OuterRecord = matchRecord,
                                               InnerRecord = groupRecord,
                                               IsFromBuffer = !isFirstOuter
                                           },
                                           JoinSource);
                    }
                }
                else if (JoinType.EmitsOuterOnMatch())
                {
                    PairCount++;

                    yield return Stamp(new AccessStep.JoinEmit(PairCount) { OuterRecord = matchRecord }, JoinSource);
                }
                else if (JoinType.EmitsInnerOnMatch() && isFirstOuter)
                {
                    foreach (var groupRecord in group)
                    {
                        PairCount++;

                        yield return Stamp(new AccessStep.JoinEmit(PairCount) { InnerRecord = groupRecord }, JoinSource);
                    }
                }

                isFirstOuter = false;

                await foreach (var step in outer.AdvanceAsync().WithCancellation(CurrentToken))
                {
                    yield return step;
                }
            }

            group.Clear();

            ResetBuffers();
        }

        // An outer join has to read out the side it preserves, which is why its inputs are not left part read like an inner join's
        if (JoinType.PreservesOuter())
        {
            while (outer.CurrentRecord is { } remaining)
            {
                MarkState(OuterSource, remaining, JoinRowState.Finished);

                yield return Stamp(Unmatched(remaining, null), JoinSource);

                await foreach (var step in outer.AdvanceAsync().WithCancellation(CurrentToken))
                {
                    yield return step;
                }
            }
        }

        if (JoinType.PreservesInner())
        {
            while (inner.CurrentRecord is { } remaining)
            {
                MarkState(InnerSource, remaining, JoinRowState.Finished);

                yield return Stamp(Unmatched(null, remaining), JoinSource);

                await foreach (var step in inner.AdvanceAsync().WithCancellation(CurrentToken))
                {
                    yield return step;
                }
            }
        }

        var reason = outer.CurrentRecord is null
            ? outer.StopReason ?? StopReason.PageExhausted
            : inner.StopReason ?? StopReason.PageExhausted;

        yield return Stamp(new AccessStep.Stopped(reason), JoinSource);
    }

    private AccessStep.JoinEmit Unmatched(IRecord? outerRecord, IRecord? innerRecord)
    {
        PairCount++;

        return new AccessStep.JoinEmit(PairCount)
        {
            OuterRecord = outerRecord,
            InnerRecord = innerRecord,
            IsUnmatched = true
        };
    }

    private static bool HasNull(AccessKey key)
    {
        for (var index = 0; index < key.Count; index++)
        {
            if (key[index].IsNull)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Drops the rows a completed pairing consumed, keeping the row each side has already read past it
    /// </summary>
    private void ResetBuffers()
    {
        OuterRows.Clear();
        InnerRows.Clear();

        if (Outer?.CurrentRecord is { } outerRecord)
        {
            OuterRows.Add(new JoinBufferRow(outerRecord, JoinRowState.Pending));
        }

        if (Inner?.CurrentRecord is { } innerRecord)
        {
            InnerRows.Add(new JoinBufferRow(innerRecord, JoinRowState.Pending));
        }
    }

    /// <summary>
    /// Takes a row the side has just returned, dropping any the join has already finished with
    /// </summary>
    /// <remarks>
    /// A row that has been paired or passed over is no longer held by the join, so it goes as soon as the walk moves on. Rows of a matched
    /// group stay because they are replayed against any further outer rows carrying the same key.
    /// </remarks>
    private void CollectRow(int source, IRecord record)
    {
        var rows = source == OuterSource ? OuterRows : InnerRows;

        rows.RemoveAll(r => r.State == JoinRowState.Finished);

        rows.Add(new JoinBufferRow(record, JoinRowState.Pending));
    }

    private void MarkMatched(int source, IRecord record)
    {
        MarkState(source, record, JoinRowState.Matched);
    }

    private void MarkState(int source, IRecord record, JoinRowState state)
    {
        var rows = source == OuterSource ? OuterRows : InnerRows;

        for (var index = 0; index < rows.Count; index++)
        {
            if (ReferenceEquals(rows[index].Record, record))
            {
                rows[index] = rows[index] with { State = state };

                return;
            }
        }
    }


    private AccessStep.MergeCompare Compare(AccessKey outerKey, AccessKey innerKey, int comparison, string action)
    {
        return new AccessStep.MergeCompare(comparison)
        {
            OuterKey = outerKey,
            InnerKey = innerKey,
            Action = action,

            // A comparison that advances one side has proven that side's row has no partner, so it is a verdict on that row
            Decision = JoinType.Decide(comparison <= 0, comparison >= 0)
        };
    }

    private static AccessKey GetKey(IRecord record, IReadOnlyList<string> columns)
    {
        var source = new RecordRowValueSource(record);

        var values = new AccessValue[columns.Count];

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];

            if (!record.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Row has no column '{column}' to build the merge key");
            }

            values[index] = source.GetValue(-1, column).WithColumnName(column);
        }

        return new AccessKey([.. values]);
    }

    private AccessStep Stamp(AccessStep step, int source)
    {
        if (source == OuterSource)
        {
            OuterCounters = step.Counters;
        }
        else if (source == InnerSource)
        {
            InnerCounters = step.Counters;
        }

        return step with { Source = source, Counters = OuterCounters.Add(InnerCounters) };
    }

    private sealed class SideCursor(IndexStepService service, int source, MergeJoinStepService owner)
    {
        public IRecord? CurrentRecord { get; private set; }

        public StopReason? StopReason { get; private set; }

        public async IAsyncEnumerable<AccessStep> AdvanceAsync()
        {
            CurrentRecord = null;

            while (true)
            {
                var step = await service.StepNextAsync(owner.CurrentToken);

                if (step is null)
                {
                    yield break;
                }

                if (step is AccessStep.Stopped stopped)
                {
                    StopReason = stopped.Reason;

                    yield break;
                }

                if (step is AccessStep.Row { EmittedRecord: { } record })
                {
                    CurrentRecord = record;

                    owner.CollectRow(source, record);

                    yield return owner.Stamp(step, source);

                    yield break;
                }

                yield return owner.Stamp(step, source);
            }
        }
    }
}
