using System.Collections.Generic;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Plan;
using Windows.UI;

namespace InternalsViewer.UI.App.Helpers;

public static class PlanNodeAnnotations
{
    private static readonly Color Pushdown = Color.FromArgb(255, 0x6C, 0x9E, 0x3F);

    private static readonly Color Filter = Color.FromArgb(255, 0x2E, 0x7D, 0xB8);

    private static readonly Color Storage = Color.FromArgb(255, 0x8A, 0x6D, 0xC0);

    private static readonly Color Hardware = Color.FromArgb(255, 0xC2, 0x8A, 0x1E);

    private static readonly Color Warning = Color.FromArgb(255, 0xC4, 0x2B, 0x1C);

    public static IReadOnlyList<PlanNodeAnnotation> For(PlanNode node)
    {
        var annotations = new List<PlanNodeAnnotation>();

        if (node.BatchInfo is { } batch)
        {
            if (batch.LocallyAggregatedRows > 0)
            {
                annotations.Add(new PlanNodeAnnotation("Aggregate Pushdown", $"{batch.LocallyAggregatedRows:N0} rows aggregated in the scan", Pushdown));
            }

            if (batch.IsLocalAggregationUsed == true)
            {
                annotations.Add(new PlanNodeAnnotation("Local Aggregation", string.Empty, Pushdown));
            }

            if (batch.IsFilterOnCompressedDataUsed == true)
            {
                annotations.Add(new PlanNodeAnnotation("Compressed Filter", "Predicate evaluated without decoding", Filter));
            }

            if (batch.IsPrefiltered == true)
            {
                annotations.Add(new PlanNodeAnnotation("Predicate Pushdown", "Rows filtered inside the scan", Filter));
            }

            if (batch.SegmentSkips > 0)
            {
                annotations.Add(new PlanNodeAnnotation("Segment Elimination", $"{batch.SegmentSkips:N0} skipped", Filter));
            }

            if (batch.IsGlobalDictionaryUsed == true)
            {
                annotations.Add(new PlanNodeAnnotation("Global Dictionary", batch.GlobalDictionaryKeyColumns ?? string.Empty, Storage));
            }

            if (batch.IsDeepDataPossible == true)
            {
                annotations.Add(new PlanNodeAnnotation("Deep Data", "Values too wide for a vector slot", Storage));
            }

            if (batch.IsFastComparisonUsed == true)
            {
                annotations.Add(new PlanNodeAnnotation("Fast Comparison", string.Empty, Hardware));
            }

            if (!string.IsNullOrEmpty(batch.CpuInstructionSet) && batch.CpuInstructionSet != "NoSimd")
            {
                annotations.Add(new PlanNodeAnnotation("SIMD", batch.CpuInstructionSet!, Hardware));
            }
        }

        if (node.PredicateInfo is { HasUntranslatedPredicate: true })
        {
            annotations.Add(new PlanNodeAnnotation("Untranslated Predicate", "The trace cannot evaluate this predicate", Warning));
        }

        if (node.PredicateInfo?.RowGoal is { } rowGoal)
        {
            annotations.Add(new PlanNodeAnnotation("Row Goal", $"{rowGoal:N0} rows", Filter));
        }

        if (node is { IsBatchMode: false, CountersByThread.Count: > 0 } && HasBatchModeSibling(node))
        {
            annotations.Add(new PlanNodeAnnotation("Row Mode", "Row mode operator over batch mode input", Warning));
        }

        if (node.MemoryGrant?.UsedKb is { } used && node.MemoryGrant?.InputKb is { } granted && granted > 0 && used > granted)
        {
            annotations.Add(new PlanNodeAnnotation("Over Grant", "Used more memory than granted", Warning));
        }

        return annotations;
    }

    private static bool HasBatchModeSibling(PlanNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.IsBatchMode)
            {
                return true;
            }
        }

        return false;
    }
}
