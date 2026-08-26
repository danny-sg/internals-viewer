using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Events.BatchMode;

public static class BatchInfoBuilder
{
    public static void Apply(IReadOnlyList<EngineEvent> events, IReadOnlyList<ExecutionPlan> plans)
    {
        var batchEvents = events.OfType<BatchModeEvent>().Cast<EngineEvent>()
                                .Concat(events.OfType<SegmentScanEvent>())
                                .ToList();

        if (batchEvents.Count == 0)
        {
            return;
        }

        var nodes = plans.SelectMany(p => p.Root.SelectMany(Flatten))
                         .GroupBy(n => n.NodeId)
                         .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var group in batchEvents.GroupBy(NodeIdOf))
        {
            if (!nodes.TryGetValue(group.Key, out var matched))
            {
                continue;
            }

            foreach (var node in matched)
            {
                node.BatchInfo ??= new BatchInfo();

                foreach (var batchEvent in group)
                {
                    Merge(node.BatchInfo, batchEvent);
                }
            }
        }
    }

    private static int NodeIdOf(EngineEvent e) => e switch
    {
        BatchModeEvent batch => batch.NodeId,
        SegmentScanEvent scan => scan.NodeId,
        _ => 0
    };

    private static void Merge(BatchInfo info, EngineEvent engineEvent)
    {
        if (engineEvent is SegmentScanEvent scan)
        {
            info.CpuInstructionSet ??= scan.CpuInstructionSet.ToString();

            info.IsFilterOnCompressedDataUsed = Or(info.IsFilterOnCompressedDataUsed, scan.IsFilterOnCompressedDataUsed);
            info.IsDeepDataPossible = Or(info.IsDeepDataPossible, scan.IsDeepDataPossible);

            if (scan.HasScanResult)
            {
                info.PureRowBuckets = Add(info.PureRowBuckets, scan.PureRowBuckets);
                info.ImpureRowBuckets = Add(info.ImpureRowBuckets, scan.ImpureRowBuckets);
            }

            return;
        }

        if (engineEvent is BatchModeEvent e)
        {
            MergeBatch(info, e);
        }
    }

    private static void MergeBatch(BatchInfo info, BatchModeEvent e)
    {
        info.IsFastComparisonUsed = Or(info.IsFastComparisonUsed, e.IsFastComparisonUsed);
        info.IsLocalAggregationUsed = Or(info.IsLocalAggregationUsed, e.IsLocalAggregationUsed);
        info.IsPrefiltered = Or(info.IsPrefiltered, e.IsPrefiltered);
        info.IsGlobalDictionaryUsed = Or(info.IsGlobalDictionaryUsed, e.IsGlobalDictionaryUsed);
        info.GlobalDictionaryKeyColumns ??= e.GlobalDictionaryKeyColumns;
    }

    private static bool? Or(bool? current, bool? value) => value is null ? current : (current ?? false) || value.Value;

    private static long? Add(long? current, long? value) => value is null ? current : (current ?? 0) + value.Value;

    private static IEnumerable<PlanNode> Flatten(PlanNode? node)
    {
        if (node is null)
        {
            yield break;
        }

        yield return node;

        foreach (var child in node.Children.SelectMany(Flatten))
        {
            yield return child;
        }
    }
}
