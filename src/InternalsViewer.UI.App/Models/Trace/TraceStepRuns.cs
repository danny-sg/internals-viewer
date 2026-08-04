using System;
using System.Collections.Generic;
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

        if (step is AccessStep.HashBuild hashBuild)
        {
            if (history.Count > 1 && history[1] is HashBuildSpan span && span.NodeId == hashBuild.NodeId)
            {
                span.Progress.Apply(hashBuild);

                return;
            }

            for (var index = 0; index < history.Count; index++)
            {
                if (history[index] is HashBuildSpan found && found.NodeId == hashBuild.NodeId)
                {
                    found.Progress.Apply(hashBuild);

                    if (index > 1)
                    {
                        history.RemoveAt(index);

                        history.Insert(1, found);
                    }

                    return;
                }
            }

            var created = new HashBuildSpan
            {
                NodeId = hashBuild.NodeId,
                Counters = hashBuild.Counters
            };

            created.Progress.Apply(hashBuild);

            history.Insert(0, created);

            return;
        }

        if (step is AccessStep.HashProbe hashProbe)
        {
            var span = FindProbeSpan(history, hashProbe.NodeId, relocate: true);

            if (span is null)
            {
                span = new HashProbeSpan
                {
                    NodeId = hashProbe.NodeId,
                    Counters = hashProbe.Counters
                };

                span.Progress.Fill = FindBuildFill(history, hashProbe.NodeId);

                history.Insert(0, span);
            }

            span.Progress.Apply(hashProbe);

            return;
        }

        if (step is AccessStep.HashCompare hashCompare && FindProbeSpan(history, hashCompare.NodeId, relocate: false) is { } compareSpan)
        {
            compareSpan.Progress.Apply(hashCompare);

            return;
        }

        if (step is AccessStep.JoinEmit joinEmit && FindProbeSpan(history, joinEmit.NodeId, relocate: false) is { } emitSpan)
        {
            emitSpan.Progress.Apply(joinEmit);

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

    private static HashProbeSpan? FindProbeSpan(ObservableCollection<AccessStep> history, int nodeId, bool relocate)
    {
        if (history.Count > 1 && history[1] is HashProbeSpan fast && fast.NodeId == nodeId)
        {
            return fast;
        }

        for (var index = 0; index < history.Count; index++)
        {
            if (history[index] is HashProbeSpan span && span.NodeId == nodeId)
            {
                if (relocate && index > 1)
                {
                    history.RemoveAt(index);

                    history.Insert(1, span);
                }

                return span;
            }
        }

        return null;
    }

    private static IReadOnlyList<int>? FindBuildFill(ObservableCollection<AccessStep> history, int nodeId)
    {
        for (var index = 0; index < history.Count; index++)
        {
            if (history[index] is HashBuildSpan build && build.NodeId == nodeId)
            {
                return build.Progress.Fill;
            }
        }

        return null;
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
