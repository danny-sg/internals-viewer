using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Interfaces.DataAccess;

namespace InternalsViewer.Internals.DataAccess.Executors;

/// <summary>
/// Executes a seek against a single page, locating an entry point then walking the range
/// </summary>
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

        yield return new AccessStep.ReadPage(page.PageAddress, page.Level, page.IsLeaf, page.SlotCount)
        {
            Counters = totals
        };

        var target = forward ? bounds.StartValue : bounds.EndValue;
        var inclusive = forward ? bounds.IsStartInclusive : bounds.IsEndInclusive;

        var entry = forward ? 0 : page.SlotCount;

        var width = 0;

        SeekRule? rule = null;

        if (!target.IsUnbounded)
        {
            width = GetWidth(bounds, target);

            rule = forward
                ? page.IsLeaf
                    ? (inclusive ? SeekRule.LowestGreaterOrEqual : SeekRule.LowestGreater)
                    : (inclusive ? SeekRule.HighestLess : SeekRule.HighestLessOrEqual)
                : (inclusive ? SeekRule.HighestLessOrEqual : SeekRule.HighestLess);
        }

        yield return new AccessStep.ProbeStart(page.SlotCount)
        {
            Rule = rule,
            Target = target,
            Width = width,
            Direction = direction,
            IsLeaf = page.IsLeaf,
            Counters = totals
        };

        if (!target.IsUnbounded)
        {
            var useLowerBound = forward ? inclusive : !inclusive;

            var (bound, probes) = useLowerBound
                ? AccessPathSearch.LowerBound(page, target, width)
                : AccessPathSearch.UpperBound(page, target, width);

            foreach (var probe in probes)
            {
                totals = Publish(totals.AddComparisons(1), onCountersChanged);

                yield return probe with { Counters = totals };
            }

            entry = !page.IsLeaf && forward ? Math.Max(0, bound - 1) : bound;
        }

        var cursor = forward ? entry : entry - 1;

        yield return new AccessStep.ProbeResult(cursor, cursor >= page.SlotCount || cursor < 0)
        {
            Rule = rule,
            Target = target,
            Width = width,
            Counters = totals
        };

        if (!page.IsLeaf)
        {
            if (cursor < 0 || cursor >= page.SlotCount)
            {
                yield return new AccessStep.Stopped(StopReason.PageExhausted) { Counters = totals };

                yield break;
            }

            yield return new AccessStep.Descend(cursor, page.GetChildPage(cursor)) { Counters = totals };

            yield break;
        }

        while (forward ? cursor < page.SlotCount : cursor >= 0)
        {
            if (page.GetRecord(cursor).IsGhost)
            {
                totals = Publish(totals.AddGhostSkipped(), onCountersChanged);

                yield return new AccessStep.Row(cursor, RowOutcome.Ghost) { Counters = totals };

                cursor += forward ? 1 : -1;

                continue;
            }

            var within = WithinTrailingBound(page, cursor, bounds, forward, out var compared, out var boundaryComparison);

            if (compared)
            {
                totals = Publish(totals.AddComparisons(1), onCountersChanged);
            }

            if (!within)
            {
                var boundary = forward ? bounds.EndValue : bounds.StartValue;

                yield return new AccessStep.RangeEnd(cursor)
                {
                    Key = page.GetKey(cursor),
                    Boundary = boundary,
                    Width = GetWidth(bounds, boundary),
                    Comparison = boundaryComparison,
                    Counters = totals
                };

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
                                            out bool compared,
                                            out int comparison)
    {
        var boundary = forward ? bounds.EndValue : bounds.StartValue;

        compared = false;
        comparison = 0;

        if (boundary.IsUnbounded)
        {
            return true;
        }

        var inclusive = forward ? bounds.IsEndInclusive : bounds.IsStartInclusive;

        var width = GetWidth(bounds, boundary);

        comparison = page.CompareKeyPrefix(slot, boundary, width);

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
