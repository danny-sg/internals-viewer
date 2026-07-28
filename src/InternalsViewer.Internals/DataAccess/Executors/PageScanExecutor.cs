using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Interfaces.DataAccess;

namespace InternalsViewer.Internals.DataAccess.Executors;

/// <summary>
/// Executes a scan against a single page, examining every slot in turn
/// </summary>
public sealed class PageScanExecutor(IRowBinder rowBinder)
{
    private IRowBinder RowBinder { get; } = rowBinder;

    public IEnumerable<AccessStep> Execute(IAccessPage page,
                                           ScanDirection direction = ScanDirection.Forward,
                                           AccessPredicate? residual = null,
                                           long? rowGoal = null,
                                           AccessCounters counters = default,
                                           Action<AccessCounters>? onCountersChanged = null)
    {
        var totals = counters;

        var forward = direction == ScanDirection.Forward;

        totals = Publish(totals.AddPageRead(), onCountersChanged);

        yield return new AccessStep.ReadPage(page.PageAddress, page.Level, page.IsLeaf, page.SlotCount)
        {
            Counters = totals
        };

        var cursor = forward ? 0 : page.SlotCount - 1;

        yield return new AccessStep.ProbeResult(cursor, page.SlotCount == 0) { Counters = totals };

        while (forward ? cursor < page.SlotCount : cursor >= 0)
        {
            if (page.GetRecord(cursor).IsGhost)
            {
                totals = Publish(totals.AddGhostSkipped(), onCountersChanged);

                yield return new AccessStep.Row(cursor, RowOutcome.Ghost) { Counters = totals };

                cursor += forward ? 1 : -1;

                continue;
            }

            totals = Publish(totals.AddRowRead(), onCountersChanged);

            var outcome = EvaluateResidual(page, cursor, residual) switch
            {
                true => RowOutcome.Match,
                false => RowOutcome.NoMatch,
                _ => RowOutcome.Unknown
            };

            if (outcome == RowOutcome.Match)
            {
                totals = Publish(totals.AddRowOutput(), onCountersChanged);
            }

            yield return new AccessStep.Row(cursor, outcome) { Counters = totals };

            if (outcome == RowOutcome.Match && totals.RowsOutput == rowGoal)
            {
                yield return new AccessStep.Stopped(StopReason.RowGoalMet) { Counters = totals };

                yield break;
            }

            cursor += forward ? 1 : -1;
        }

        yield return new AccessStep.Stopped(StopReason.PageExhausted) { Counters = totals };
    }

    private static AccessCounters Publish(AccessCounters counters, Action<AccessCounters>? onCountersChanged)
    {
        onCountersChanged?.Invoke(counters);

        return counters;
    }

    private bool? EvaluateResidual(IAccessPage page, int slot, AccessPredicate? residual)
    {
        if (residual is null or AccessPredicate.True)
        {
            return true;
        }

        return PredicateEvaluator.Evaluate(residual, RowBinder.Bind(page, slot));
    }
}
