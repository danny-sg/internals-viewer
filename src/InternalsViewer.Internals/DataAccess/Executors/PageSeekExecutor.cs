using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Interfaces.DataAccess;

namespace InternalsViewer.Internals.DataAccess.Executors;

/// <summary>
/// Executes a seek against a single page, locating an entry point then walking the range
/// </summary>
/// <remarks>
/// The walk stops at the end of the page rather than following a leaf link, so this represents
/// the part of a seek that happens once the leaf has been reached.
/// </remarks>
public sealed class PageSeekExecutor(IRowBinder rowBinder)
{
    private IRowBinder RowBinder { get; } = rowBinder;

    public IEnumerable<AccessStep> Execute(IIndexAccessPage page,
                                           SeekBounds bounds,
                                           ScanDirection direction,
                                           AccessPredicate? residual = null,
                                           long? rowGoal = null,
                                           AccessCounters counters = default,
                                           Action<AccessCounters>? onCountersChanged = null)
    {
        return Walk(page, bounds, direction, residual, rowGoal, counters, onCountersChanged);
    }

    private IEnumerable<AccessStep> Walk(IIndexAccessPage page,
                                         SeekBounds bounds,
                                         ScanDirection direction,
                                         AccessPredicate? residual,
                                         long? rowGoal,
                                         AccessCounters totals,
                                         Action<AccessCounters>? onCountersChanged)
    {
        var forward = direction == ScanDirection.Forward;

        totals = Publish(totals.AddPageRead(), onCountersChanged);

        yield return new AccessStep.EnterPage(page.PageAddress, page.Level, page.IsLeaf, page.SlotCount)
        {
            Counters = totals
        };

        var target = forward ? bounds.StartKey : bounds.EndKey;
        var inclusive = forward ? bounds.StartInclusive : bounds.EndInclusive;

        var entry = forward ? 0 : page.SlotCount;

        if (!target.IsUnbounded)
        {
            var width = GetWidth(bounds, target);

            var (bound, probes) = inclusive
                ? AccessPathSearch.LowerBound(page, target, width)
                : AccessPathSearch.UpperBound(page, target, width);

            foreach (var probe in probes)
            {
                totals = Publish(totals.AddComparisons(1), onCountersChanged);

                yield return probe with { Counters = totals };
            }

            entry = bound;
        }

        var cursor = forward ? entry : entry - 1;

        yield return new AccessStep.EntryPoint(cursor, cursor >= page.SlotCount || cursor < 0)
        {
            Counters = totals
        };

        while (forward ? cursor < page.SlotCount : cursor >= 0)
        {
            if (page.GetRecord(cursor).IsGhost)
            {
                totals = Publish(totals.AddGhostSkipped(), onCountersChanged);

                yield return new AccessStep.Row(cursor, RowOutcome.Ghost) { Counters = totals };

                cursor += forward ? 1 : -1;

                continue;
            }

            var within = WithinTrailingBound(page, cursor, bounds, forward, out var compared);

            if (compared)
            {
                totals = Publish(totals.AddComparisons(1), onCountersChanged);
            }

            if (!within)
            {
                yield return new AccessStep.RangeEnd(cursor) { Counters = totals };
                yield return new AccessStep.Stopped(StopReason.RangeEnded) { Counters = totals };

                yield break;
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

    private static int GetWidth(SeekBounds bounds, in AccessKey target)
    {
        return bounds.CompareWidth == int.MaxValue ? target.Count : bounds.CompareWidth;
    }

    private static bool WithinTrailingBound(IIndexAccessPage page,
                                            int slot,
                                            SeekBounds bounds,
                                            bool forward,
                                            out bool compared)
    {
        var boundary = forward ? bounds.EndKey : bounds.StartKey;

        compared = false;

        if (boundary.IsUnbounded)
        {
            return true;
        }

        var inclusive = forward ? bounds.EndInclusive : bounds.StartInclusive;

        var width = GetWidth(bounds, boundary);

        var comparison = page.CompareKeyPrefix(slot, boundary, width);

        compared = true;

        if (comparison == 0)
        {
            return inclusive;
        }

        return forward ? comparison < 0 : comparison > 0;
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
