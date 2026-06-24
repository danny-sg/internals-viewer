using System;
using System.Collections.Generic;
using InternalsViewer.Query.Plans;

namespace InternalsViewer.UI.App.Controls.Plan;

public static class PlanIconResolver
{
    private const string IconBase = "ms-appx:///Controls/Plan/Icons/";

    private static readonly HashSet<string> KnownIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClusteredIndexScan", "ClusteredIndexSeek", "ClusteredIndexInsert", "ClusteredIndexUpdate",
        "ClusteredIndexDelete", "IndexScan", "IndexSeek", "IndexInsert", "IndexUpdate", "IndexDelete",
        "TableScan", "TableInsert", "TableUpdate", "TableDelete", "RIDLookup", "KeyLookup",
        "NestedLoops", "HashMatch", "HashAggregate", "MergeJoin", "Sort", "StreamAggregate",
        "ComputeScalar", "Filter", "Top", "TopN", "Parallelism", "Concatenation", "ConstantScan",
        "TableSpool", "IndexSpool", "RowCountSpool", "Spool", "Segment", "Sequence", "SequenceProject",
        "Assert", "Bitmap", "Split", "Collapse", "MergeInterval", "WindowAggregate", "WindowSpool",
        "Result", "Default"
    };

    private static readonly Dictionary<string, string> LogicalOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Key Lookup"] = "KeyLookup",
        ["RID Lookup"] = "RIDLookup",
    };

    private static readonly Dictionary<string, string> StatementIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SELECT"] = "Select",
        ["INSERT"] = "Insert",
        ["UPDATE"] = "Update",
        ["DELETE"] = "Delete",
        ["MERGE"] = "Merge",
    };

    public static Uri Resolve(PlanNode node) => new(IconBase + ResolveAssetName(node) + ".svg");

    private static string ResolveAssetName(PlanNode node)
    {
        if (node.IsStatement)
        {
            return StatementIcons.GetValueOrDefault(node.PhysicalOperator, "Statement");
        }

        if (!string.IsNullOrEmpty(node.LogicalOperator)
            && LogicalOverrides.TryGetValue(node.LogicalOperator, out var logicalIcon))
        {
            return logicalIcon;
        }

        var asset = Compact(node.PhysicalOperator);

        return KnownIcons.Contains(asset) ? asset : "Default";
    }

    private static string Compact(string operatorName)
        => string.IsNullOrEmpty(operatorName)
            ? "Default"
            : operatorName.Replace(" ", string.Empty).Replace("/", string.Empty);
}
