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
                                 EvaluationContext? evaluationContext = null)
    {
        OuterColumns = outerInput.JoinColumns;
        InnerColumns = innerInput.JoinColumns;
        CompareWidth = Math.Min(OuterColumns.Count, InnerColumns.Count);
        ComparisonSign = outerInput.Direction == ScanDirection.Backward ? -1 : 1;

        OuterCounters = default;
        InnerCounters = default;
        PairCount = 0;
        IsComplete = false;

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
        var outer = new SideCursor(OuterService, OuterSource, this);

        var inner = new SideCursor(InnerService, InnerSource, this);

        var keys = string.Join(", ", OuterColumns.Zip(InnerColumns, (o, i) => $"{o} = {i}"));

        yield return Stamp(new AccessStep.JoinStart($"Merge join on {keys}. Reading the first outer row"), JoinSource);

        await foreach (var step in outer.AdvanceAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }

        yield return Stamp(new AccessStep.JoinStart("Reading the first inner row"), JoinSource);

        await foreach (var step in inner.AdvanceAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }

        while (outer.CurrentRecord is { } outerRecord && inner.CurrentRecord is { } innerRecord)
        {
            var outerKey = GetKey(outerRecord, OuterColumns);

            var innerKey = GetKey(innerRecord, InnerColumns);

            var comparison = outerKey.ComparePrefix(innerKey, CompareWidth) * ComparisonSign;

            if (comparison < 0)
            {
                yield return Stamp(Compare(outerKey, innerKey, comparison, "Outer key is behind: advance outer"), JoinSource);

                await foreach (var step in outer.AdvanceAsync())
                {
                    yield return step;
                }

                continue;
            }

            if (comparison > 0)
            {
                yield return Stamp(Compare(outerKey, innerKey, comparison, "Inner key is behind: advance inner"), JoinSource);

                await foreach (var step in inner.AdvanceAsync().WithCancellation(CurrentToken))
                {
                    yield return step;
                }

                continue;
            }

            yield return Stamp(Compare(outerKey, innerKey, comparison, "Keys match: collect the inner group"), JoinSource);

            var group = new List<IRecord>();

            var groupKey = innerKey;

            while (inner.CurrentRecord is { } groupRecord && GetKey(groupRecord, InnerColumns).ComparePrefix(groupKey, CompareWidth) == 0)
            {
                group.Add(groupRecord);

                await foreach (var step in inner.AdvanceAsync().WithCancellation(CurrentToken))
                {
                    yield return step;
                }
            }

            var isFirstOuter = true;

            while (outer.CurrentRecord is { } matchRecord && GetKey(matchRecord, OuterColumns).ComparePrefix(groupKey, CompareWidth) == 0)
            {
                foreach (var groupRecord in group)
                {
                    PairCount++;

                    yield return Stamp(new AccessStep.JoinEmit(PairCount)
                    {
                        OuterRecord = matchRecord,
                        InnerRecord = groupRecord,
                        IsFromBuffer = !isFirstOuter
                    }, JoinSource);
                }

                isFirstOuter = false;

                await foreach (var step in outer.AdvanceAsync())
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

    private static AccessStep.MergeCompare Compare(AccessKey outerKey, AccessKey innerKey, int comparison, string action)
    {
        return new AccessStep.MergeCompare(comparison)
        {
            OuterKey = outerKey,
            InnerKey = innerKey,
            Action = action
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

                    yield return owner.Stamp(step, source);

                    yield break;
                }

                yield return owner.Stamp(step, source);
            }
        }
    }
}
