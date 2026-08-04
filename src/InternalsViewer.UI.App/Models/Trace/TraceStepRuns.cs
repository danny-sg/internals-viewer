using System;
using System.Collections.ObjectModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.UI.App.Models.Trace;

public static class TraceStepRuns
{
    public static void Append(AccessStep step, ObservableCollection<AccessStep> history, int historyLimit)
    {
        if (step is AccessStep.MergeCompare { Comparison: not 0 } compare
            && history.Count > 1
            && history[0] is AccessStep.Row or AccessStep.RowRun)
        {
            if (history[1] is AccessStep.MergeCompare previous
                && Math.Sign(previous.Comparison) == Math.Sign(compare.Comparison)
                && StaticKeyMatches(compare.Comparison, previous.OuterKey, previous.InnerKey, compare))
            {
                history[1] = new AccessStep.MergeCompareRun(Math.Sign(compare.Comparison), 2)
                {
                    OuterFrom = previous.OuterKey,
                    OuterTo = compare.OuterKey,
                    InnerFrom = previous.InnerKey,
                    InnerTo = compare.InnerKey,
                    Action = compare.Action,
                    NodeId = compare.NodeId,
                    Counters = compare.Counters
                };

                return;
            }

            if (history[1] is AccessStep.MergeCompareRun run
                && run.Comparison == Math.Sign(compare.Comparison)
                && StaticKeyMatches(compare.Comparison, run.OuterTo, run.InnerTo, compare))
            {
                history[1] = run with
                {
                    Count = run.Count + 1,
                    OuterTo = compare.OuterKey,
                    InnerTo = compare.InnerKey,
                    Counters = compare.Counters
                };

                return;
            }
        }

        if (step is AccessStep.HashBuild hashBuild
            && history.Count > 1
            && history[0] is AccessStep.Row or AccessStep.RowRun)
        {
            if (history[1] is AccessStep.HashBuild previousBuild && previousBuild.NodeId == hashBuild.NodeId)
            {
                var fill = SeedFill(history, hashBuild);

                Mark(fill, previousBuild.BucketCount, previousBuild.Bucket, previousBuild.ChainLength, hashBuild.BucketCount);
                Mark(fill, hashBuild.BucketCount, hashBuild.Bucket, hashBuild.ChainLength, hashBuild.BucketCount);

                history[1] = new AccessStep.HashBuildRun(hashBuild.Bucket, hashBuild.Hash, 2)
                {
                    Key = hashBuild.Key,
                    ChainLength = hashBuild.ChainLength,
                    IsNullKey = hashBuild.IsNullKey,
                    BucketCount = hashBuild.BucketCount,
                    BucketFill = fill,
                    NodeId = hashBuild.NodeId,
                    Counters = hashBuild.Counters
                };

                return;
            }

            if (history[1] is AccessStep.HashBuildRun buildRun && buildRun.NodeId == hashBuild.NodeId)
            {
                var fill = buildRun.BucketCount == hashBuild.BucketCount && buildRun.BucketFill is int[] existing
                    ? existing
                    : new int[hashBuild.BucketCount];

                Mark(fill, hashBuild.BucketCount, hashBuild.Bucket, hashBuild.ChainLength, hashBuild.BucketCount);

                history[1] = buildRun with
                {
                    Bucket = hashBuild.Bucket,
                    Hash = hashBuild.Hash,
                    Count = buildRun.Count + 1,
                    Key = hashBuild.Key,
                    ChainLength = hashBuild.ChainLength,
                    IsNullKey = hashBuild.IsNullKey,
                    BucketCount = hashBuild.BucketCount,
                    BucketFill = fill,
                    Counters = hashBuild.Counters
                };

                return;
            }
        }

        if (step is AccessStep.HashProbe hashProbe && FindProbeRun(history, hashProbe) is { } probeIndex)
        {
            history[probeIndex] = history[probeIndex] is AccessStep.HashProbeRun hashProbeRun
                ? hashProbeRun with
                {
                    Count = hashProbeRun.Count + 1,
                    Counters = hashProbe.Counters
                }
                : new AccessStep.HashProbeRun(hashProbe.Bucket, hashProbe.Hash, 2)
                {
                    Key = hashProbe.Key,
                    ChainLength = hashProbe.ChainLength,
                    IsNullKey = hashProbe.IsNullKey,
                    NodeId = hashProbe.NodeId,
                    Counters = hashProbe.Counters
                };

            return;
        }

        if (step is AccessStep.Probe probe)
        {
            if (history.Count > 0 && history[0] is AccessStep.ProbeRun probeRun && probeRun.NodeId == probe.NodeId)
            {
                history[0] = new AccessStep.ProbeRun([probe, .. probeRun.Probes])
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

        if (step is AccessStep.Row row && history.Count > 0)
        {
            var latest = history[0];

            if (latest is AccessStep.Row previous
                && previous.NodeId == row.NodeId
                && previous.Outcome == row.Outcome
                && previous.IsReadAhead == row.IsReadAhead
                && Math.Abs(row.Slot - previous.Slot) == 1)
            {
                history[0] = new AccessStep.RowRun(previous.Slot, row.Slot, row.Outcome)
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
                history[0] = run with
                {
                    ToSlot = row.Slot,
                    Count = run.Count + 1,
                    EmitCount = run.EmitCount + EmitOf(row),
                    Counters = row.Counters
                };

                return;
            }
        }

        history.Insert(0, step);

        if (history.Count > historyLimit)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    /// <summary>
    /// Finds the probe of the same key that this one repeats, or null where the last probe was of something else
    /// </summary>
    /// <remarks>
    /// A probe row that found candidates leaves its comparisons and whatever it emitted behind it, and the row that carried the next key
    /// lands on top of those, so the probe before is not the entry beneath. Anything other than that work ends the search, a probe of a
    /// different key most of all, so a run only ever gathers probes that ran one after another.
    /// </remarks>
    private static int? FindProbeRun(ObservableCollection<AccessStep> history, AccessStep.HashProbe probe)
    {
        for (var index = 0; index < history.Count; index++)
        {
            var previous = history[index];

            if (previous is AccessStep.Row or AccessStep.RowRun or AccessStep.HashCompare or AccessStep.JoinEmit)
            {
                continue;
            }

            var key = previous switch
            {
                AccessStep.HashProbe found => found.Key,
                AccessStep.HashProbeRun run => run.Key,
                _ => (AccessKey?)null
            };

            return key is { } previousKey && previous.NodeId == probe.NodeId && previousKey.Equals(probe.Key) ? index : null;
        }

        return null;
    }

    private static int[] SeedFill(ObservableCollection<AccessStep> history, AccessStep.HashBuild build)
    {
        for (var index = 2; index < history.Count; index++)
        {
            if (history[index] is AccessStep.HashBuildRun run && run.NodeId == build.NodeId)
            {
                return run.BucketCount == build.BucketCount && run.BucketFill is int[] fill
                    ? (int[])fill.Clone()
                    : new int[build.BucketCount];
            }
        }

        return new int[build.BucketCount];
    }

    private static void Mark(int[] fill, int bucketCount, int bucket, int chainLength, int currentBucketCount)
    {
        if (bucketCount == currentBucketCount && bucket >= 0 && bucket < fill.Length)
        {
            fill[bucket] = chainLength;
        }
    }

    private static int EmitOf(AccessStep.Row row)
    {
        return row.Outcome == RowOutcome.Match ? 1 : 0;
    }

    private static bool StaticKeyMatches(int comparison, AccessKey previousOuter, AccessKey previousInner, AccessStep.MergeCompare compare)
    {
        return comparison < 0 ? previousInner.Equals(compare.InnerKey) : previousOuter.Equals(compare.OuterKey);
    }
}
