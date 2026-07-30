using InternalsViewer.Internals.DataAccess.AccessPaths;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.Interfaces.DataAccess;

namespace InternalsViewer.Internals.DataAccess.Executors;

public static class AllocationScanExecutor
{
    public static IEnumerable<AccessStep> Execute(IRowPageAccessor page,
                                                  AccessPredicate? residual = null,
                                                  long? rowGoal = null,
                                                  AccessCounters counters = default,
                                                  Action<AccessCounters>? onCountersChanged = null,
                                                  EvaluationContext? evaluationContext = null,
                                                  bool isHeap = false)
    {
        return Walk(page, residual, rowGoal, counters, onCountersChanged, evaluationContext ?? EvaluationContext.Now, isHeap);
    }

    private static IEnumerable<AccessStep> Walk(IRowPageAccessor page,
                                                AccessPredicate? residual,
                                                long? rowGoal,
                                                AccessCounters totals,
                                                Action<AccessCounters>? onCountersChanged,
                                                EvaluationContext evaluationContext,
                                                bool isHeap)
    {
        var hasResidual = residual is not (null or AccessPredicate.True);

        totals = Publish(totals.AddPageRead(), onCountersChanged);

        yield return new AccessStep.ReadPage(page.PageAddress, page.Level, false, page.IsLeaf, page.SlotCount)
        {
            IsHeap = isHeap,
            Counters = totals
        };

        for (var slot = 0; slot < page.SlotCount; slot++)
        {
            if (page.GetRecord(slot).IsGhost)
            {
                totals = Publish(totals.AddGhostSkipped(), onCountersChanged);

                yield return new AccessStep.Row(slot, RowOutcome.Ghost) { HasResidual = hasResidual, HasRange = false, Counters = totals };

                continue;
            }

            totals = Publish(totals.AddRowRead(), onCountersChanged);

            var outcome = EvaluateResidual(page, slot, residual, evaluationContext) switch
            {
                true => RowOutcome.Match,
                false => RowOutcome.NoMatch,
                _ => RowOutcome.Unknown
            };

            if (outcome == RowOutcome.Match)
            {
                totals = Publish(totals.AddRowOutput(), onCountersChanged);
            }

            yield return new AccessStep.Row(slot, outcome)
            {
                HasResidual = hasResidual,
                HasRange = false,
                EmittedRecord = outcome == RowOutcome.Match ? RecordSnapshot.Detach(page.GetRecord(slot)) : null,
                Counters = totals
            };

            if (outcome == RowOutcome.Match && totals.RowsOutput == rowGoal)
            {
                yield return new AccessStep.Stopped(StopReason.RowGoalMet) { Counters = totals };

                yield break;
            }
        }
    }

    private static AccessCounters Publish(AccessCounters counters, Action<AccessCounters>? onCountersChanged)
    {
        onCountersChanged?.Invoke(counters);

        return counters;
    }

    private static bool? EvaluateResidual(IRowPageAccessor page, int slot, AccessPredicate? residual, EvaluationContext evaluationContext)
    {
        if (residual is null or AccessPredicate.True)
        {
            return true;
        }

        return PredicateEvaluator.Evaluate(residual, page.BindRow(slot), evaluationContext);
    }
}
