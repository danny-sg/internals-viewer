using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Trace;

public static class TraceStepRuns
{
    public static void Append(AccessStep step, ObservableCollection<AccessStep> history, int historyLimit)
    {
        if (step is AccessStep.Stopped or AccessStep.Close or AccessStep.Sorted)
        {
            RetireSpans(history, step.NodeId);
        }

        var top = LeadingSpans(history);

        if (step is AccessStep.MergeCompare compare)
        {
            var span = FindSpan<MergeCompareSpan>(history, top, compare.NodeId);

            if (compare.Comparison == 0)
            {
                if (span is not null)
                {
                    span.IsComplete = true;
                }

                MergeMatch(history, compare).Progress.Apply(compare);

                return;
            }

            if (span is not null && span.Progress.Direction != Math.Sign(compare.Comparison))
            {
                span.IsComplete = true;

                span = null;
            }

            if (span is null)
            {
                span = new MergeCompareSpan
                {
                    NodeId = compare.NodeId,
                    Counters = compare.Counters
                };

                InsertSpan(history, span);
            }

            span.Progress.Apply(compare);

            return;
        }

        if (step is AccessStep.HashBuild hashBuild)
        {
            if (FindSpan<HashBuildSpan>(history, top, hashBuild.NodeId) is { } buildSpan)
            {
                buildSpan.Progress.Apply(hashBuild);

                return;
            }

            var created = new HashBuildSpan
            {
                NodeId = hashBuild.NodeId,
                Counters = hashBuild.Counters
            };

            created.Progress.Apply(hashBuild);

            InsertSpan(history, created);

            return;
        }

        if (step is AccessStep.HashProbe hashProbe)
        {
            var span = FindSpan<HashProbeSpan>(history, top, hashProbe.NodeId);

            if (span is null)
            {
                span = new HashProbeSpan
                {
                    NodeId = hashProbe.NodeId,
                    Counters = hashProbe.Counters
                };

                if (FindSpan<HashBuildSpan>(history, top, hashProbe.NodeId) is { } buildSpan)
                {
                    span.Progress.Fill = buildSpan.Progress.Fill;

                    buildSpan.IsComplete = true;
                }

                InsertSpan(history, span);
            }

            span.Progress.Apply(hashProbe);

            return;
        }

        if (step is AccessStep.HashCompare hashCompare && FindSpan<HashProbeSpan>(history, top, hashCompare.NodeId) is { } compareSpan)
        {
            compareSpan.Progress.Apply(hashCompare);

            if (hashCompare.IsMatch)
            {
                MatchSpan(history, top, hashCompare).Progress.Apply(hashCompare);
            }

            return;
        }

        if (step is AccessStep.JoinEmit joinEmit)
        {
            if (FindSpan<HashProbeSpan>(history, top, joinEmit.NodeId) is { } emitSpan)
            {
                emitSpan.Progress.Apply(joinEmit);

                MatchSpan(history, top, joinEmit).Progress.Apply(joinEmit);

                return;
            }

            if (FindSpan<MergeMatchSpan>(history, top, joinEmit.NodeId) is { } mergeMatch)
            {
                mergeMatch.Progress.Apply(joinEmit);

                return;
            }

            if (FindSpan<MergeCompareSpan>(history, top, joinEmit.NodeId) is not null)
            {
                MergeMatch(history, joinEmit).Progress.Apply(joinEmit);

                return;
            }
        }

        if (step is AccessStep.TopRow topRow)
        {
            var span = FindSpan<RowCountSpan>(history, top, topRow.NodeId);

            if (span is null)
            {
                span = new RowCountSpan
                {
                    NodeId = topRow.NodeId,
                    Counters = topRow.Counters,
                    Badge = "→ Emit"
                };

                InsertSpan(history, span);
            }

            span.Progress.Apply(topRow.Number, topRow.RowCount);

            return;
        }

        if (step is AccessStep.Output output)
        {
            var span = FindSpan<RowCountSpan>(history, top, output.NodeId);

            if (span is null)
            {
                span = new RowCountSpan
                {
                    NodeId = output.NodeId,
                    Counters = output.Counters,
                    Badge = "→ Client"
                };

                InsertSpan(history, span);
            }

            span.Progress.Apply(output.Number, 0);

            return;
        }

        if (step is AccessStep.SortCollect sortCollect)
        {
            var span = FindSpan<SortCollectSpan>(history, top, sortCollect.NodeId);

            if (span is null)
            {
                span = new SortCollectSpan
                {
                    NodeId = sortCollect.NodeId,
                    Counters = sortCollect.Counters
                };

                InsertSpan(history, span);
            }

            span.Progress.Apply(sortCollect.Number, 0);

            return;
        }

        if (step is AccessStep.SortRow sortRow)
        {
            var span = FindSpan<RowCountSpan>(history, top, sortRow.NodeId);

            if (span is null)
            {
                span = new RowCountSpan
                {
                    NodeId = sortRow.NodeId,
                    Counters = sortRow.Counters,
                    Badge = "→ Emit"
                };

                InsertSpan(history, span);
            }

            span.Progress.Apply(sortRow.Number, 0);

            return;
        }

        if (step is AccessStep.SortDuplicate sortDuplicate
            && history.Count > top
            && history[top] is AccessStep.SortDuplicate previousDuplicate
            && previousDuplicate.NodeId == sortDuplicate.NodeId)
        {
            history[top] = sortDuplicate with { Count = previousDuplicate.Count + 1 };

            return;
        }

        if (step is AccessStep.ConcatRow concatRow)
        {
            var span = FindSpan<RowCountSpan>(history, top, concatRow.NodeId);

            if (span is null)
            {
                span = new RowCountSpan
                {
                    NodeId = concatRow.NodeId,
                    Counters = concatRow.Counters,
                    Badge = "→ Emit"
                };

                InsertSpan(history, span);
            }

            span.Progress.Apply(concatRow.Number, 0);

            return;
        }

        if (step is AccessStep.Probe probe)
        {
            if (history.Count > top && history[top] is AccessStep.ProbeRun probeRun && probeRun.NodeId == probe.NodeId)
            {
                history[top] = new AccessStep.ProbeRun([probe, .. probeRun.Probes])
                {
                    NodeId = probe.NodeId,
                    Counters = probe.Counters
                };

                return;
            }

            step = new AccessStep.ProbeRun([probe])
            {
                NodeId = probe.NodeId,
                Counters = probe.Counters
            };
        }

        if (step is AccessStep.Row row && history.Count > top)
        {
            var latest = history[top];

            if (latest is AccessStep.Row previous
                && previous.NodeId == row.NodeId
                && previous.Outcome == row.Outcome
                && previous.IsReadAhead == row.IsReadAhead
                && Math.Abs(row.Slot - previous.Slot) == 1)
            {
                history[top] = new AccessStep.RowRun(previous.Slot, row.Slot, row.Outcome)
                {
                    Count = 2,
                    HasResidual = row.HasResidual,
                    HasRange = row.HasRange,
                    EmitCount = EmitOf(previous) + EmitOf(row),
                    Counters = row.Counters,
                    NodeId = row.NodeId
                };

                return;
            }

            if (latest is AccessStep.RowRun run
                && run.NodeId == row.NodeId
                && run.Outcome == row.Outcome
                && !row.IsReadAhead
                && Math.Abs(row.Slot - run.ToSlot) == 1)
            {
                history[top] = run with
                {
                    ToSlot = row.Slot,
                    Count = run.Count + 1,
                    EmitCount = run.EmitCount + EmitOf(row),
                    Counters = row.Counters
                };

                return;
            }
        }

        history.Insert(top, step);

        if (history.Count > historyLimit)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    private static bool IsSpan(AccessStep step) => step is ITraceSpan { IsComplete: false };

    private static int Rank(AccessStep step) => step switch
    {
        HashMatchSpan or MergeMatchSpan => 0,
        HashProbeSpan or MergeCompareSpan => 1,
        _ => 2
    };

    private static void RetireSpans(ObservableCollection<AccessStep> history, int nodeId)
    {
        for (var index = 0; index < history.Count && history[index] is ITraceSpan; index++)
        {
            if (history[index].NodeId == nodeId && history[index] is ITraceSpan span)
            {
                span.IsComplete = true;
            }
        }
    }

    private static int LeadingSpans(ObservableCollection<AccessStep> history)
    {
        var count = 0;

        while (count < history.Count && IsSpan(history[count]))
        {
            count++;
        }

        return count;
    }

    private static T? FindSpan<T>(ObservableCollection<AccessStep> history, int top, int nodeId) where T : AccessStep
    {
        for (var index = 0; index < top; index++)
        {
            if (history[index] is T span && span.NodeId == nodeId)
            {
                return span;
            }
        }

        return null;
    }

    private static void InsertSpan(ObservableCollection<AccessStep> history, AccessStep span)
    {
        var index = 0;

        while (index < history.Count
               && IsSpan(history[index])
               && (history[index].NodeId < span.NodeId
                   || (history[index].NodeId == span.NodeId && Rank(history[index]) < Rank(span))))
        {
            index++;
        }

        history.Insert(index, span);
    }

    private static MergeMatchSpan MergeMatch(ObservableCollection<AccessStep> history, AccessStep step)
    {
        var top = LeadingSpans(history);

        if (FindSpan<MergeMatchSpan>(history, top, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new MergeMatchSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters
        };

        InsertSpan(history, created);

        return created;
    }

    private static HashMatchSpan MatchSpan(ObservableCollection<AccessStep> history, int top, AccessStep step)
    {
        if (FindSpan<HashMatchSpan>(history, top, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new HashMatchSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters
        };

        InsertSpan(history, created);

        return created;
    }

    private static int EmitOf(AccessStep.Row row)
    {
        return row.Outcome == RowOutcome.Match ? 1 : 0;
    }
}
