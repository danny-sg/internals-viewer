using System;
using System.Collections.ObjectModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.UI.App.Models.Query.Simulation.Steps;

namespace InternalsViewer.UI.App.Services.Query.Simulation.Steps;

public static class SimulationStepRuns
{
    private const string EmitBadge = "\u2192 Emit";

    private const string EmitRowsBadge = "\u2192 Emit Rows";

    public static void Append(AccessStep step, ObservableCollection<AccessStep> history, int historyLimit)
    {
        if (step is AccessStep.CompressedDataFilterBitmap)
        {
            return;
        }

        if (step is AccessStep.Stopped or AccessStep.Close or AccessStep.Sorted)
        {
            RetireSpans(history, step.NodeId);
        }

        if (step is AccessStep.AggregateEmit)
        {
            RetireSpan(history, step.NodeId, "Accumulate");
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
                return Rows(topRow, EmitBadge, topRow.Number, topRow.RowCount, history);

            case AccessStep.Output output:
                return Rows(output, "→ Client", output.Number, 0, history);

            case AccessStep.SortRow sortRow:
                return Rows(sortRow, EmitBadge, sortRow.Number, 0, history);

            case AccessStep.ConcatRow concatRow:
                return Rows(concatRow, EmitBadge, concatRow.Number, 0, history);

            case AccessStep.AggregateEmit aggregateEmit:
                return Rows(aggregateEmit, EmitBadge, aggregateEmit.Number, 0, history);

            case AccessStep.ComputeRow computeRow:
                return Rows(computeRow, EmitBadge, computeRow.Number, 0, history);

            case AccessStep.FilterRow filterRow:
                return Rows(filterRow, "→ Pass", filterRow.PassedCount, filterRow.Number, history);

            case AccessStep.SortCollect sortCollect:
                SpanFor(sortCollect, "Collect", history)
                    .Set("Row", sortCollect.Number)
                    .Set("→ Sort Table", null, CounterDisplayType.Badge, SimulationCounterColours.Success);
                return true;

            case AccessStep.AggregateRow aggregateRow:
                Accumulate(aggregateRow, history).Set("Values", aggregateRow.Running, CounterDisplayType.Pill);
                return true;

            case AccessStep.HashAggregate hashAggregate:
                var hashed = Accumulate(hashAggregate, history);

                hashed.Set("Bucket", (long)hashAggregate.Bucket)
                      .Set("Values", hashAggregate.Running, CounterDisplayType.Pill);

                if (hashAggregate.IsNewGroup)
                {
                    hashed.Increment("Groups");
                }

                hashed.Fill.Land(hashAggregate.Bucket, hashAggregate.ChainLength, hashAggregate.BucketCount);
                return true;

            case AccessStep.HashAggregateBatch batch:
                var accumulate = Accumulate(batch, history);

                accumulate.Set("Batches", batch.Number)
                          .Set("Groups", batch.Groups)
                          .Set("Values", batch.Running, CounterDisplayType.Pill);

                accumulate.Fill.Set(batch.Fill);
                return true;

            case AccessStep.SegmentRow segmentRow:
                var segment = SpanFor(segmentRow, "Segment", history);

                segment.Increment("Rows");

                segment.Set("Segments", segmentRow.SegmentCount)
                       .Set("Key", segmentRow.Key, CounterDisplayType.Pill);
                return true;

            case AccessStep.RankRow rankRow:
                SpanFor(rankRow, "Rank", history)
                    .Set("Row", rankRow.Number)
                    .Set("Values", rankRow.Values, CounterDisplayType.Pill);
                return true;

            case AccessStep.BatchProduced produced:
                var batchSpan = SpanFor(produced, "Get Batch", history);

                batchSpan.Set("Batch", produced.Number)
                         .Add("Rows", produced.RowCount)
                         .Add("Match", produced.QualifyingCount, CounterDisplayType.Badge)
                         .Add("Filter Operations", produced.FilterOperations)
                         .Add("RLE Entries", produced.FilterRleEntries)
                         .Add("Pure", produced.PureColumns)
                         .Add("Impure", produced.ImpureColumns);

                Filters(batchSpan, produced.HasCompressedFilter, produced.HasPredicate);
                return true;

            case AccessStep.BatchSkipped skipped:
                var skippedSpan = SpanFor(skipped, "Get Batch", history);

                skippedSpan.Add("Rows", skipped.RowCount)
                           .Add("Filter Operations", skipped.FilterOperations)
                           .Add("RLE Entries", skipped.FilterRleEntries)
                           .Add("Skipped", 1, CounterDisplayType.Badge);

                Filters(skippedSpan, skipped.HasCompressedFilter, skipped.HasPredicate);
                return true;

            case AccessStep.AggregatePushdown pushdown:
                var down = SpanFor(pushdown, "Aggregate Pushdown", history);

                down.Add("Rows", pushdown.RowCount)
                    .Set("Groups", pushdown.Groups);

                if (pushdown.IsRunFolded)
                {
                    down.Increment("Runs");
                }
                else
                {
                    down.Add("Rows Probed", pushdown.RowCount);
                }
                return true;

            case AccessStep.DeleteBitmapApplied deleted:
                SpanFor(deleted, "Apply Deletes", history)
                    .Set("Row Group", (long)deleted.RowGroupId)
                    .Add("Rows Deleted", deleted.Count);
                return true;

            case AccessStep.ComputeVector computeVector:
                SpanFor(computeVector, "Compute Vector", history)
                    .Set("Columns", computeVector.Columns, CounterDisplayType.Pill)
                    .Add("Rows Computed", computeVector.RowCount);
                return true;

            case AccessStep.FilterVector filterVector:
                SpanFor(filterVector, "Filter Vector", history)
                    .Set("Columns", filterVector.Columns, CounterDisplayType.Pill)
                    .Add("Rows Evaluated", filterVector.RowsEvaluated)
                    .Add("Selected", filterVector.Matches);
                return true;

            case AccessStep.BatchFiltered batchFiltered:
                SpanFor(batchFiltered, "Get Batch", history)
                    .Set("Batch", batchFiltered.Number)
                    .Set("Rows", batchFiltered.RowCount)
                    .Set("Passed", batchFiltered.PassedCount, CounterDisplayType.Pill);
                return true;

            default:
                return false;
        }
    }

    private static bool Rows(AccessStep step, string badge, long rows, long limit, ObservableCollection<AccessStep> history)
    {
        var span = SpanFor(step, "Get Row", history);

        span.Set("Row", rows).Set(badge, null, CounterDisplayType.Badge, SimulationCounterColours.Success);

        if (limit > 0)
        {
            span.Set("Of", limit);
        }

        return true;
    }

    private static void Filters(SimulationCounterSpan span, bool hasCompressedFilter, bool hasPredicate)
    {
        if (hasCompressedFilter)
        {
            span.SetOnce("Compressed Filter", null, CounterDisplayType.Badge, SimulationCounterColours.Success);
        }

        if (hasPredicate)
        {
            span.SetOnce("Predicate", null, CounterDisplayType.Badge, SimulationCounterColours.Caution);
        }

        if (!hasCompressedFilter && !hasPredicate)
        {
            span.SetOnce("No Filter", null, CounterDisplayType.Badge);
        }
    }

    private static SimulationCounterSpan Accumulate(AccessStep step, ObservableCollection<AccessStep> history)
    {
        var span = SpanFor(step, "Accumulate", history);

        span.Increment("Rows");

        return span;
    }

    private static bool FoldMergeCompare(AccessStep.MergeCompare compare, ObservableCollection<AccessStep> history, int top)
    {
        var span = FindSpan(history, top, compare.NodeId, "Compare");

        if (compare.Comparison == 0)
        {
            if (span is not null)
            {
                span.IsComplete = true;
            }

            MergeMatch(history, compare).Set("Key", compare.OuterKey).Increment("Pair");

            return true;
        }

        var direction = Math.Sign(compare.Comparison);

        if (span is not null && span.Number("Direction") != direction)
        {
            span.IsComplete = true;

            span = null;
        }

        span ??= Insert(compare, "Compare", history);

        span.Set("Direction", (long)direction);

        var moved = direction < 0 ? compare.OuterKey : compare.InnerKey;

        span.SetOnce("Advance", direction < 0 ? "Outer" : "Inner", CounterDisplayType.Pill)
            .SetOnce("From", moved)
            .Set("To", moved)
            .Set("Against", direction < 0 ? compare.InnerKey : compare.OuterKey)
            .Increment("Compares", CounterDisplayType.Badge);

        return true;
    }

    private static bool FoldHashBuild(AccessStep.HashBuild hashBuild, ObservableCollection<AccessStep> history, int top)
    {
        var span = FindSpan(history, top, hashBuild.NodeId, "Build") ?? Insert(hashBuild, "Build", history);

        span.Set("Bucket", (long)hashBuild.Bucket)
            .Set("Hash", $"0x{hashBuild.Hash:X8}")
            .Set("Chain Entry Item", (long)hashBuild.ChainLength)
            .Increment("Rows", CounterDisplayType.Badge);

        span.Fill.Land(hashBuild.Bucket, hashBuild.ChainLength, hashBuild.BucketCount);

        return true;
    }

    private static bool FoldHashProbe(AccessStep.HashProbe hashProbe, ObservableCollection<AccessStep> history, int top)
    {
        var span = FindSpan(history, top, hashProbe.NodeId, "Probe");

        if (span is null)
        {
            span = Insert(hashProbe, "Probe", history);

            if (FindSpan(history, top, hashProbe.NodeId, "Build") is { } buildSpan)
            {
                span.Fill.Set(buildSpan.Fill.Buckets);

                buildSpan.IsComplete = true;
            }
        }

        span.Set("Bucket", (long)hashProbe.Bucket)
            .Set("Hash", $"0x{hashProbe.Hash:X8}")
            .Increment("Rows", CounterDisplayType.Badge);

        span.Fill.Touch(hashProbe.Bucket, false);

        return true;
    }

    private static bool FoldHashCompare(AccessStep.HashCompare hashCompare, ObservableCollection<AccessStep> history, int top)
    {
        if (FindSpan(history, top, hashCompare.NodeId, "Probe") is not { } span)
        {
            return false;
        }

        span.Increment("Compares");

        if (hashCompare.IsMatch)
        {
            span.Increment("Matches");

            span.Fill.Touch(hashCompare.Bucket, true);

            MatchSpan(history, top, hashCompare)
                .Set("Bucket", (long)hashCompare.Bucket)
                .Set("Entry", (long)hashCompare.Entry)
                .Increment("Pair");
        }

        return true;
    }

    private static bool FoldJoinEmit(AccessStep.JoinEmit joinEmit, ObservableCollection<AccessStep> history, int top)
    {
        if (FindSpan(history, top, joinEmit.NodeId, "Probe") is { } emitSpan)
        {
            emitSpan.Increment("Emits");

            MatchSpan(history, top, joinEmit).Increment(EmitRowsBadge, CounterDisplayType.Badge, SimulationCounterColours.Success);

            return true;
        }

        if (FindSpan(history, top, joinEmit.NodeId, "Match") is { } mergeMatch)
        {
            mergeMatch.Increment(EmitRowsBadge, CounterDisplayType.Badge, SimulationCounterColours.Success);

            return true;
        }

        if (FindSpan(history, top, joinEmit.NodeId, "Compare") is not null)
        {
            MergeMatch(history, joinEmit).Increment(EmitRowsBadge, CounterDisplayType.Badge, SimulationCounterColours.Success);

            return true;
        }

        return false;
    }

    private static SimulationCounterSpan SpanFor(AccessStep step, string label, ObservableCollection<AccessStep> history)
        => FindOpenSpan(history, step.NodeId, label) ?? Insert(step, label, history);

    private static SimulationCounterSpan Insert(AccessStep step, string label, ObservableCollection<AccessStep> history)
    {
        var created = new SimulationCounterSpan
        {
            NodeId = step.NodeId,
            Counters = step.Counters,
            Label = label
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

    private static bool IsSpan(AccessStep step) => step is ISimulationSpan { IsComplete: false };

    private static int Rank(AccessStep step) => step is SimulationCounterSpan { Label: var label }
        ? label switch
        {
            "Match" => 0,
            "Probe" or "Compare" => 1,
            _ => 2
        }
        : 2;

    private static void RetireSpan(ObservableCollection<AccessStep> history, int nodeId, string label)
    {
        for (var index = 0; index < history.Count && history[index] is ISimulationSpan; index++)
        {
            if (history[index] is SimulationCounterSpan span && span.NodeId == nodeId && span.Label == label)
            {
                span.IsComplete = true;
            }
        }
    }

    private static void RetireSpans(ObservableCollection<AccessStep> history, int nodeId)
    {
        for (var index = 0; index < history.Count && history[index] is ISimulationSpan; index++)
        {
            if (history[index].NodeId == nodeId && history[index] is ISimulationSpan span)
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

    private static SimulationCounterSpan? FindOpenSpan(ObservableCollection<AccessStep> history, int nodeId, string label)
    {
        for (var index = 0; index < history.Count && history[index] is ISimulationSpan; index++)
        {
            if (history[index] is SimulationCounterSpan { IsComplete: false } span && span.NodeId == nodeId && span.Label == label)
            {
                return span;
            }
        }

        return null;
    }

    private static SimulationCounterSpan? FindSpan(ObservableCollection<AccessStep> history, int top, int nodeId, string label)
    {
        for (var index = 0; index < top; index++)
        {
            if (history[index] is SimulationCounterSpan span && span.NodeId == nodeId && span.Label == label)
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

    private static SimulationCounterSpan MergeMatch(ObservableCollection<AccessStep> history, AccessStep step)
    {
        var top = LeadingSpans(history);

        return FindSpan(history, top, step.NodeId, "Match") ?? Insert(step, "Match", history);
    }

    private static SimulationCounterSpan MatchSpan(ObservableCollection<AccessStep> history, int top, AccessStep step)
        => FindSpan(history, top, step.NodeId, "Match") ?? Insert(step, "Match", history);

    private static int EmitOf(AccessStep.Row row)
    {
        return row.Outcome == RowOutcome.Match ? 1 : 0;
    }
}
