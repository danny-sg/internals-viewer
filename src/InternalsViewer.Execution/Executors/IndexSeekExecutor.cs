using InternalsViewer.Execution.AccessPaths;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;

namespace InternalsViewer.Execution.Executors;

/// <summary>
/// Executes a seek against a single page, locating an entry point then walking the range
/// </summary>
public static class IndexSeekExecutor
{
    public static IEnumerable<AccessStep> Execute(IIndexPageAccessor page,
                                                  SeekBounds bounds,
                                                  ScanDirection direction,
                                                  AccessPredicate? residual = null,
                                                  long? rowGoal = null,
                                                  bool isContinuation = false,
                                                  AccessCounters counters = default,
                                                  Action<AccessCounters>? onCountersChanged = null,
                                                  EvaluationContext? evaluationContext = null)
    {
        return Walk(page, 
                    bounds, 
                    direction, 
                    residual, 
                    rowGoal, 
                    isContinuation, 
                    counters, 
                    onCountersChanged,
                    evaluationContext ?? EvaluationContext.Now);
    }

    /// <summary>
    /// Page Seek tree walk to seek in index/b-tree
    /// </summary>
    /// <remarks>
    /// The walk is an IEnumerable that yields AccessStep results is it steps through the page.
    ///
    /// 
    ///                                   Read page, emit AccessStep.ReadPage
    ///                                                    |
    ///                                                    v
    ///                                          isContinuation == true? --------------------------------+
    ///                                                 |                                                |
    ///                                                 No                                              Yes
    ///                                                 |                                                |
    ///                                                 v                                                |
    ///                        Determine seek target / bound from SeekBounds + direction                 |
    ///                            (forward -> StartValue, backward -> EndValue)                         |
    ///                                                 |                                                |
    ///                                                 v                                                |
    ///                            emit AccessStep.ProbeStart(rule, target, width)                       |
    ///                                                 |                                                |
    ///                                                 v                                                |
    ///                    +---------------- target unbounded? -------------------+                      |
    ///                    |                                                      |                      |
    ///                   No                                                     Yes                     |
    ///                    |                                                      |                      |
    ///                    v                                                      |                      |
    ///    AccessPathSearch.LowerBound / UpperBound                               |                      |
    ///         -> binary search on page                                          |                      |
    ///         -> emit Probe steps                                               |                      |
    ///         -> accumulate comparisons                                         |                      |
    ///                    |                                                      |                      |
    ///                    v                                                      |                      |
    ///   compute entry cursor from bound                                         |                      |
    ///                   |                                                       |                      |
    ///                   +-------------------------------------------------------+                      |
    ///                                               |                                                  |
    ///                                               v                                                  |
    ///                                       emit AccessStep.ProbeResult                                |
    ///                                               |                                                  |
    ///                                               v                                                  |
    ///                                          page.IsLeaf? --------------------------+                |
    ///                                               |                                 |                |
    ///                                 No (root/intermediate page)                    Yes               |
    ///                                               |                                 |                |
    ///                                               v                                 |                |
    ///                        +---- slot cursor out of range? -------+                 |                |
    ///                        |                                      |                 |                |
    ///                       Yes                                    No                 |                |
    ///                        |                                      |                 |                |
    ///                        v                                      v                 |                |
    ///               Stopped (PageExhausted)              emit AccessStep.Descend      |                |
    ///                   [yield break]                        [yield break]            |                |
    ///                                                 caller recurses on child page   |                |
    ///                                                                                 |                |
    ///                                                                                 +----------------+
    ///                                                                                         |
    ///                                                                                         v
    ///                                  +-----------------------------------------------------------------------------+
    ///                                  |                       Row-scanning loop (leaf page only)                    |
    ///                                  +-----------------------------------------------------------------------------+
    ///                                                                  |
    ///                                                                  v
    ///                      ----------------------->cursor within slot range (forward/backward)? -----------------------+
    ///                      |                                           |                                               |
    ///                      |                                          Yes                                              No
    ///                      |                                           |                                               |
    ///                      |                                           v                                               v
    ///                      |                               record is ghost? --------------+                  Stopped (PageExhausted)
    ///                      |                                    |                         |                       [yield break]
    ///                      |                                   Yes                        No
    ///                      |                                    |                         |
    ///                      |                                    v                         v
    ///                      |                         emit Row(Ghost)      within trailing bound (RangeEnd check)?
    ///                      |                         advance slot cursor                  |
    ///                      |                              |                +--------------+-----------------+
    ///                      |                              |               No                                Yes
    ///                      |                              |                |                                 |
    ///                      |                              |                v                                 v
    ///                      |                              |   Emit RangeEnd +evaluate                 residual predicate
    ///                      |                              |    Stopped (RangeEnded)                          |
    ///                      |                              |        [yield break]                             v
    ///                      |                              |                                   emit Row(Match/NoMatch/Unknown)
    ///                      |                              |                                              |
    ///                      |                              |                                              v
    ///                      |                              |                                      rowGoal reached
    ///                      |                              |                                       (Match count)? -------------+
    ///                      |                              |                                         |                         |
    ///                      |                              |                                        No                        Yes
    ///                      |                              |                                         |                         |
    ///                      |                              |                                         v                         v
    ///                      |                              |                                 advance slot cursor      Stopped (RowGoalMet)
    ///                      |                              |                                      (+1 / -1)               [yield break]
    ///                      |                              |                                          |
    ///                      |                              +------------------------------------------+
    ///                      |                                                  |
    ///                      |                                                  v
    ///                      +------------------------------ loop back to 'slot cursor within slot range?'
    /// </remarks>
    private static IEnumerable<AccessStep> Walk(IIndexPageAccessor page,
                                                SeekBounds bounds,
                                                ScanDirection direction,
                                                AccessPredicate? residual,
                                                long? rowGoal,
                                                bool isContinuation,
                                                AccessCounters totals,
                                                Action<AccessCounters>? onCountersChanged,
                                                EvaluationContext evaluationContext)
    {
        var forward = direction == ScanDirection.Forward;

        var hasResidual = residual is not (null or AccessPredicate.True);

        totals = Publish(totals.AddPageRead(), onCountersChanged);

        yield return new AccessStep.ReadPage(page.PageAddress, page.Level, page.IsRoot, page.IsLeaf, page.SlotCount)
        {
            Counters = totals
        };

        var cursor = forward ? 0 : page.SlotCount - 1;

        if (!isContinuation)
        {
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

            cursor = forward ? entry : entry - 1;

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
        }

        while (forward ? cursor < page.SlotCount : cursor >= 0)
        {
            if (page.GetRecord(cursor).IsGhost)
            {
                totals = Publish(totals.AddGhostSkipped(), onCountersChanged);

                yield return new AccessStep.Row(cursor, RowOutcome.Ghost) { HasResidual = hasResidual, Counters = totals };

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

            var outcome = EvaluateResidual(page, cursor, residual, evaluationContext) switch
            {
                true => RowOutcome.Match,
                false => RowOutcome.NoMatch,
                _ => RowOutcome.Unknown
            };

            if (outcome == RowOutcome.Match)
            {
                totals = Publish(totals.AddRowOutput(), onCountersChanged);
            }

            yield return new AccessStep.Row(cursor, outcome)
            {
                HasResidual = hasResidual,
                EmittedRecord = outcome == RowOutcome.Match ? RecordSnapshot.Detach(page.GetRecord(cursor)) : null,
                Counters = totals
            };

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

    /// <summary>
    /// Checks if the key for the page/slot is within the trailing bound of the seek bounds
    /// </summary>
    /// <remarks>
    /// Depends on the direction of the seek:
    ///
    /// Forward  -> End Value
    /// Backward -> Start Value
    ///     
    /// </remarks>
    private static bool WithinTrailingBound(IIndexPageAccessor page,
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

    private static bool? EvaluateResidual(IIndexPageAccessor page, 
                                          int slot, 
                                          AccessPredicate? residual, 
                                          EvaluationContext evaluationContext)
    {
        if (residual is null or AccessPredicate.True)
        {
            return true;
        }

        return PredicateEvaluator.Evaluate(residual, page.BindRow(slot), evaluationContext);
    }
}
