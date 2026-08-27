using System;
using System.Collections.ObjectModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.UI.App.Models.Query.Trace.Steps;

namespace InternalsViewer.UI.App.Services.Query.Trace.Steps;

public static class TraceStepRuns
{
    public static void Append(AccessStep step, ObservableCollection<AccessStep> history, int historyLimit)
    {
        if (step is AccessStep.Stopped or AccessStep.Close or AccessStep.Sorted)
        {
            RetireSpans(history, step.NodeId);
        }

        if (step is AccessStep.AggregateEmit)
        {
            RetireSpan<StreamAggregateSpan>(history, step.NodeId);
        }

        var top = LeadingSpans(history);

        if (TryFoldIntoSpan(step, history, top))
        {
            return;
        }

        if (step is AccessStep.SortDuplicate sortDuplicate && TryExtendSortDuplicate(sortDuplicate, history, top))
        {
            return;
        }

        if (step is AccessStep.Probe probe)
        {
            if (TryExtendProbeRun(probe, history, top))
            {
                return;
            }

            step = new AccessStep.ProbeRun([probe])
            {
                NodeId = probe.NodeId,
                Counters = probe.Counters
            };
        }

        if (step is AccessStep.Row row && TryExtendRowRun(row, history, top))
        {
            return;
        }

        history.Insert(top, step);

        if (history.Count > historyLimit)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    private static bool TryFoldIntoSpan(AccessStep step, ObservableCollection<AccessStep> history, int top)
    {
        switch (step)
        {
            case AccessStep.MergeCompare compare:
                return FoldMergeCompare(compare, history, top);

            case AccessStep.HashBuild hashBuild:
                return FoldHashBuild(hashBuild, history, top);

            case AccessStep.HashProbe hashProbe:
                return FoldHashProbe(hashProbe, history, top);

            case AccessStep.HashCompare hashCompare:
                return FoldHashCompare(hashCompare, history, top);

            case AccessStep.JoinEmit joinEmit:
                return FoldJoinEmit(joinEmit, history, top);

            case AccessStep.TopRow topRow:
                RowCountSpanFor(topRow, "→ Emit", history, top).Progress.Apply(topRow.Number, topRow.RowCount);
                return true;

            case AccessStep.Output output:
                RowCountSpanFor(output, "→ Client", history, top).Progress.Apply(output.Number, 0);
                return true;

            case AccessStep.SortCollect sortCollect:
                SortCollectSpanFor(sortCollect, history, top).Progress.Apply(sortCollect.Number, 0);
                return true;

            case AccessStep.SortRow sortRow:
                RowCountSpanFor(sortRow, "→ Emit", history, top).Progress.Apply(sortRow.Number, 0);
                return true;

            case AccessStep.ConcatRow concatRow:
                RowCountSpanFor(concatRow, "→ Emit", history, top).Progress.Apply(concatRow.Number, 0);
                return true;

            case AccessStep.AggregateRow aggregateRow:
                StreamAggregateSpanFor(aggregateRow, history, top).Progress.Apply(aggregateRow);
                return true;

            case AccessStep.HashAggregate hashAggregate:
                StreamAggregateSpanFor(hashAggregate, history, top).Progress.Apply(hashAggregate);
                return true;

            case AccessStep.AggregateEmit aggregateEmit:
                RowCountSpanFor(aggregateEmit, "→ Emit", history, top).Progress.Apply(aggregateEmit.Number, 0);
                return true;

            case AccessStep.ComputeRow computeRow:
                RowCountSpanFor(computeRow, "→ Emit", history, top).Progress.Apply(computeRow.Number, 0);
                return true;

            case AccessStep.SegmentRow segmentRow:
                SegmentSpanFor(segmentRow, history).Progress.Apply(segmentRow);
                return true;

            case AccessStep.RankRow rankRow:
                RankSpanFor(rankRow, history).Progress.Apply(rankRow);
                return true;

            case AccessStep.BatchProduced batchProduced:
                var batchSpan = BatchCountSpanFor(batchProduced, "→ Batch", history, top);

                batchSpan.Progress.Apply(batchProduced.Number, batchProduced.RowCount);

                batchSpan.Work.Apply(batchProduced);
                return true;

            case AccessStep.FilterVector filterVector:
                BatchFilterSpanFor(filterVector, history).Progress.Apply(filterVector);
                return true;

            case AccessStep.BatchFiltered batchFiltered:
                BatchGetSpanFor(batchFiltered, history).Progress.Apply(batchFiltered);
                return true;

            case AccessStep.FilterRow filterRow:
                RowCountSpanFor(filterRow, RowCountSpan.PassBadge, history, top)
                    .Progress.Apply(filterRow.PassedCount, filterRow.Number);
                return true;

            default:
                return false;
        }
    }

    private static bool FoldMergeCompare(AccessStep.MergeCompare compare, ObservableCollection<AccessStep> history, int top)
    {
        var span = FindSpan<MergeCompareSpan>(history, top, compare.NodeId);

        if (compare.Comparison == 0)
        {
            span?.IsComplete = true;

            MergeMatch(history, compare).Progress.Apply(compare);

            return true;
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

        return true;
    }

    private static bool FoldHashBuild(AccessStep.HashBuild hashBuild, ObservableCollection<AccessStep> history, int top)
    {
        if (FindSpan<HashBuildSpan>(history, top, hashBuild.NodeId) is { } buildSpan)
        {
            buildSpan.Progress.Apply(hashBuild);

            return true;
        }

        var created = new HashBuildSpan
        {
            NodeId = hashBuild.NodeId,
            Counters = hashBuild.Counters
        };

        created.Progress.Apply(hashBuild);

        InsertSpan(history, created);

        return true;
    }

    private static bool FoldHashProbe(AccessStep.HashProbe hashProbe, ObservableCollection<AccessStep> history, int top)
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

        return true;
    }

    private static bool FoldHashCompare(AccessStep.HashCompare hashCompare, ObservableCollection<AccessStep> history, int top)
    {
        if (FindSpan<HashProbeSpan>(history, top, hashCompare.NodeId) is not { } span)
        {
            return false;
        }

        span.Progress.Apply(hashCompare);

        if (hashCompare.IsMatch)
        {
            MatchSpan(history, top, hashCompare).Progress.Apply(hashCompare);
        }

        return true;
    }

    private static bool FoldJoinEmit(AccessStep.JoinEmit joinEmit, ObservableCollection<AccessStep> history, int top)
    {
        if (FindSpan<HashProbeSpan>(history, top, joinEmit.NodeId) is { } emitSpan)
        {
            emitSpan.Progress.Apply(joinEmit);

            MatchSpan(history, top, joinEmit).Progress.Apply(joinEmit);

            return true;
        }

        if (FindSpan<MergeMatchSpan>(history, top, joinEmit.NodeId) is { } mergeMatch)
        {
            mergeMatch.Progress.Apply(joinEmit);

            return true;
        }

        if (FindSpan<MergeCompareSpan>(history, top, joinEmit.NodeId) is not null)
        {
            MergeMatch(history, joinEmit).Progress.Apply(joinEmit);

            return true;
        }

        return false;
    }

    private static RowCountSpan RowCountSpanFor(AccessStep step, string badge, ObservableCollection<AccessStep> history, int top)
    {
        if (FindOpenSpan<RowCountSpan>(history, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new RowCountSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters,
            Badge = badge
        };

        InsertSpan(history, created);

        return created;
    }

    private static BatchCountSpan BatchCountSpanFor(AccessStep step,
                                                    string badge,
                                                    ObservableCollection<AccessStep> history,
                                                    int top)
    {
        if (FindOpenSpan<BatchCountSpan>(history, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new BatchCountSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters,
            Badge = badge
        };

        InsertSpan(history, created);

        return created;
    }

    private static BatchFilterSpan BatchFilterSpanFor(AccessStep step, ObservableCollection<AccessStep> history)
    {
        if (FindOpenSpan<BatchFilterSpan>(history, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new BatchFilterSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters
        };

        InsertSpan(history, created);

        return created;
    }

    private static BatchGetSpan BatchGetSpanFor(AccessStep step, ObservableCollection<AccessStep> history)
    {
        if (FindOpenSpan<BatchGetSpan>(history, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new BatchGetSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters
        };

        InsertSpan(history, created);

        return created;
    }

    private static SegmentSpan SegmentSpanFor(AccessStep step, ObservableCollection<AccessStep> history)
    {
        if (FindOpenSpan<SegmentSpan>(history, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new SegmentSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters
        };

        InsertSpan(history, created);

        return created;
    }

    private static RankSpan RankSpanFor(AccessStep step, ObservableCollection<AccessStep> history)
    {
        if (FindOpenSpan<RankSpan>(history, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new RankSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters
        };

        InsertSpan(history, created);

        return created;
    }

    private static SortCollectSpan SortCollectSpanFor(AccessStep step, ObservableCollection<AccessStep> history, int top)
    {
        if (FindSpan<SortCollectSpan>(history, top, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new SortCollectSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters
        };

        InsertSpan(history, created);

        return created;
    }

    private static StreamAggregateSpan StreamAggregateSpanFor(AccessStep step, ObservableCollection<AccessStep> history, int top)
    {
        if (FindOpenSpan<StreamAggregateSpan>(history, step.NodeId) is { } span)
        {
            return span;
        }

        var created = new StreamAggregateSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters
        };

        InsertSpan(history, created);

        return created;
    }

    private static bool TryExtendSortDuplicate(AccessStep.SortDuplicate sortDuplicate, ObservableCollection<AccessStep> history, int top)
    {
        if (history.Count > top
            && history[top] is AccessStep.SortDuplicate previousDuplicate
            && previousDuplicate.NodeId == sortDuplicate.NodeId)
        {
            history[top] = sortDuplicate with { Count = previousDuplicate.Count + 1 };

            return true;
        }

        return false;
    }

    private static bool TryExtendProbeRun(AccessStep.Probe probe, ObservableCollection<AccessStep> history, int top)
    {
        if (history.Count > top && history[top] is AccessStep.ProbeRun probeRun && probeRun.NodeId == probe.NodeId)
        {
            history[top] = new AccessStep.ProbeRun([probe, .. probeRun.Probes])
            {
                NodeId = probe.NodeId,
                Counters = probe.Counters
            };

            return true;
        }

        return false;
    }

    private static bool TryExtendRowRun(AccessStep.Row row, ObservableCollection<AccessStep> history, int top)
    {
        if (history.Count <= top)
        {
            return false;
        }

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

            return true;
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

            return true;
        }

        return false;
    }

    private static bool IsSpan(AccessStep step) => step is ITraceSpan { IsComplete: false };

    private static int Rank(AccessStep step) => step switch
    {
        HashMatchSpan or MergeMatchSpan => 0,
        HashProbeSpan or MergeCompareSpan => 1,
        BatchGetSpan => 1,
        _ => 2
    };

    private static void RetireSpan<T>(ObservableCollection<AccessStep> history, int nodeId)
        where T : AccessStep, ITraceSpan
    {
        for (var index = 0; index < history.Count && history[index] is ITraceSpan; index++)
        {
            if (history[index] is T span && span.NodeId == nodeId)
            {
                span.IsComplete = true;
            }
        }
    }

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

    private static T? FindOpenSpan<T>(ObservableCollection<AccessStep> history, int nodeId)
        where T : AccessStep, ITraceSpan
    {
        for (var index = 0; index < history.Count && history[index] is ITraceSpan; index++)
        {
            if (history[index] is T { IsComplete: false } span && span.NodeId == nodeId)
            {
                return span;
            }
        }

        return null;
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
