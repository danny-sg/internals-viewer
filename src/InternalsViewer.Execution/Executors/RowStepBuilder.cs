using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Interfaces.Pages;
using InternalsViewer.Execution.Records;

namespace InternalsViewer.Execution.Executors;

/// <summary>
/// Builds Row Access Steps
/// </summary>
/// <remarks>
/// Evaluates the residual predicate, adding to the row output if matched, and stops the walk when the row goal has been met.
///
/// A ghost row is skipped without the predicate being evaluated, counting against ghosts skipped rather than rows read.
/// </remarks>
internal static class RowStepBuilder
{
    public static AccessStep.Row Ghost(PageWalk walk, int slot, AccessCounters totals, bool hasRange)
    {
        return new AccessStep.Row(slot, RowOutcome.Ghost)
        {
            HasResidual = walk.HasResidual,
            HasRange = hasRange,
            Counters = totals.AddGhostSkipped()
        };
    }

    public static IEnumerable<AccessStep> Examine(IRowPageAccessor page,
                                                  PageWalk walk,
                                                  int slot,
                                                  AccessCounters totals,
                                                  bool hasRange)
    {
        totals = totals.AddRowRead();

        var outcome = walk.Evaluate(page, slot) switch
        {
            true => RowOutcome.Match,
            false => RowOutcome.NoMatch,
            _ => RowOutcome.Unknown
        };

        if (outcome == RowOutcome.Match)
        {
            totals = totals.AddRowOutput();
        }

        yield return new AccessStep.Row(slot, outcome)
        {
            HasResidual = walk.HasResidual,
            HasRange = hasRange,
            EmittedRecord = outcome == RowOutcome.Match ? RecordSnapshot.Detach(page.GetRecord(slot)) : null,
            Counters = totals
        };

        if (outcome == RowOutcome.Match && totals.RowsOutput == walk.RowGoal)
        {
            yield return new AccessStep.Stopped(StopReason.RowGoalMet) { Counters = totals };
        }
    }
}
