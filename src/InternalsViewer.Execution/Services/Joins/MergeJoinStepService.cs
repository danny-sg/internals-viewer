using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Execution.Services.Joins.Definitions;
using InternalsViewer.Execution.Services.Joins.Inputs;
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
public sealed class MergeJoinStepService(IndexStepService outerService, IndexStepService innerService) : JoinStepService
{
    public override PageAddress? CurrentPageAddress
        => Current?.Source == InnerSource ? Inner.Service.CurrentPageAddress : Outer.Service.CurrentPageAddress;

    private SideCursor? OuterCursor { get; set; }

    private SideCursor? InnerCursor { get; set; }

    private List<IRecord> Group { get; } = [];

    private IReadOnlyList<string> OuterColumns { get; set; } = [];

    private IReadOnlyList<string> InnerColumns { get; set; } = [];

    private int CompareWidth { get; set; }

    private int ComparisonSign { get; set; } = 1;

    private AccessCounters OuterCounters { get; set; }

    private AccessCounters InnerCounters { get; set; }

    private CancellationToken CurrentToken { get; set; }

    private IAsyncEnumerator<AccessStep>? Steps { get; set; }

    public async Task StartAsync(DatabaseSource database,
                                 MergeSideDefinition outerInput,
                                 MergeSideDefinition innerInput,
                                 CancellationToken cancellationToken,
                                 EvaluationContext? evaluationContext = null,
                                 JoinType joinType = JoinType.Inner)
    {
        var outer = new IndexScan(outerService, outerInput);

        var inner = new IndexScan(innerService, innerInput);

        Outer = outer;
        Inner = inner;

        ResetJoin(joinType);

        OuterColumns = outerInput.JoinColumns;
        InnerColumns = innerInput.JoinColumns;
        CompareWidth = Math.Min(OuterColumns.Count, InnerColumns.Count);
        ComparisonSign = outerInput.Direction == ScanDirection.Backward ? -1 : 1;

        OuterCounters = default;
        InnerCounters = default;

        OuterCursor = null;
        InnerCursor = null;

        Group.Clear();

        if (Steps is not null)
        {
            await Steps.DisposeAsync();
        }

        await outer.StartAsync(database, cancellationToken, evaluationContext);

        await inner.StartAsync(database, cancellationToken, evaluationContext);

        Steps = Run().GetAsyncEnumerator(CancellationToken.None);
    }

    public override async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
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
        var outer = OuterCursor = new SideCursor(Outer, OuterSource, this);

        var inner = InnerCursor = new SideCursor(Inner, InnerSource, this);

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

                Outer.MarkState(outerRecord, JoinRowState.Finished);

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

                Inner.MarkState(innerRecord, JoinRowState.Finished);

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

            Outer.MarkMatched(outerRecord);
            Inner.MarkMatched(innerRecord);

            yield return Stamp(Compare(outerKey, innerKey, comparison, "Outer = Inner"), JoinSource);

            var group = Group;

            group.Clear();

            var groupKey = innerKey;

            while (inner.CurrentRecord is { } groupRecord && GetKey(groupRecord, InnerColumns).ComparePrefix(groupKey, CompareWidth) == 0)
            {
                group.Add(groupRecord);

                Inner.MarkMatched(groupRecord);

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
                Outer.MarkMatched(matchRecord);

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
                Outer.MarkState(remaining, JoinRowState.Finished);

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
                Inner.MarkState(remaining, JoinRowState.Finished);

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
        Outer.Clear();
        Inner.Clear();

        if (OuterCursor?.CurrentRecord is { } outerRecord)
        {
            Outer.Hold(outerRecord, JoinRowState.Pending);
        }

        if (InnerCursor?.CurrentRecord is { } innerRecord)
        {
            Inner.Hold(innerRecord, JoinRowState.Pending);
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

    private sealed class SideCursor(JoinInput input, int source, MergeJoinStepService owner)
    {
        public IRecord? CurrentRecord { get; private set; }

        public StopReason? StopReason { get; private set; }

        public async IAsyncEnumerable<AccessStep> AdvanceAsync()
        {
            CurrentRecord = null;

            while (true)
            {
                var step = await input.Service.StepNextAsync(owner.CurrentToken);

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

                    input.Collect(record);

                    yield return owner.Stamp(step, source);

                    yield break;
                }

                yield return owner.Stamp(step, source);
            }
        }
    }
}
