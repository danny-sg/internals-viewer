using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Helpers;

public static class PlanNodePropertyBuilder
{
    public static List<PlanNodeProperty> Build(PlanNode node)
    {
        var result = new List<PlanNodeProperty>();

        var operatorGroup = new PlanNodeProperty("Operator", string.Empty);

        operatorGroup.Children.Add(new PlanNodeProperty("Physical Operator", node.PhysicalOperator));
        operatorGroup.Children.Add(new PlanNodeProperty("Logical Operator", node.LogicalOperator));
        operatorGroup.Children.Add(new PlanNodeProperty("Node Id", node.NodeId.ToString(CultureInfo.InvariantCulture)));

        result.Add(operatorGroup);

        var optimizerGroup = new PlanNodeProperty("Optimizer", string.Empty);

        optimizerGroup.Children.Add(new PlanNodeProperty("Rows Output", node.RowsOutput.ToString("N0", CultureInfo.InvariantCulture)));

        if (node.RowsRead is { } rowsRead)
        {
            optimizerGroup.Children.Add(new PlanNodeProperty("Rows Read", rowsRead.ToString("N0", CultureInfo.InvariantCulture))
            {
                IsValueHighlighted = rowsRead != node.RowsOutput
            });
        }

        if (node.EstimatedCost is { } cost)
        {
            optimizerGroup.Children.Add(new PlanNodeProperty("Estimated Subtree Cost", cost.ToString("0.#######", CultureInfo.InvariantCulture)));
        }

        result.Add(optimizerGroup);

        if (!string.IsNullOrEmpty(node.Table))
        {
            var storageGroup = new PlanNodeProperty("Storage", string.Empty);

            storageGroup.Children.Add(new PlanNodeProperty("Object", ObjectName(node)) { IsValueMonospace = true });

            if (node.ScanInfo is { } scanInfo)
            {
                if (scanInfo.IsOutputOrdered is { } ordered)
                {
                    storageGroup.Children.Add(BoolProperty("Ordered", ordered));
                }

                storageGroup.Children.Add(new PlanNodeProperty("Direction", scanInfo.IsForward == false ? "Backward" : "Forward"));

                if (scanInfo.IsLookup)
                {
                    storageGroup.Children.Add(BoolProperty("Lookup", true));
                }

                if (scanInfo.IsForcedIndex)
                {
                    storageGroup.Children.Add(BoolProperty("Forced Index", true));
                }

                if (scanInfo.IsForceSeek)
                {
                    storageGroup.Children.Add(BoolProperty("Force Seek", true));
                }

                if (scanInfo.IsForceScan)
                {
                    storageGroup.Children.Add(BoolProperty("Force Scan", true));
                }
            }

            result.Add(storageGroup);
        }

        if (node.PredicateInfo is { } predicateInfo)
        {
            var predicateGroup = new PlanNodeProperty("Predicates", string.Empty);

            var hasPredicate = predicateInfo.Residual is not null || predicateInfo.HasUntranslatedPredicate;

            if (node.ScanInfo is not null && (predicateInfo.HasSeekBounds || hasPredicate))
            {
                predicateGroup.Children.Add(new PlanNodeProperty("SARGable", predicateInfo.HasSeekBounds ? "Yes" : "No")
                {
                    IsValueSuccess = predicateInfo.HasSeekBounds,
                    IsValueError = !predicateInfo.HasSeekBounds
                });
            }

            if (predicateInfo.HasSeekBounds)
            {
                var ranges = new PlanNodeProperty("Seek Predicates", string.Empty);

                for (var index = 0; index < predicateInfo.SeekBounds.Length; index++)
                {
                    var bounds = predicateInfo.SeekBounds[index];

                    ranges.Children.Add(new PlanNodeProperty($"Range {index + 1}", PredicateWriter.ToText(PredicateWriter.Write(bounds)))
                    {
                        Predicate = PredicateText.From(bounds)
                    });
                }

                predicateGroup.Children.Add(ranges);
            }

            if (predicateInfo.Residual is { } residual)
            {
                predicateGroup.Children.Add(new PlanNodeProperty("Predicate", PredicateWriter.ToText(PredicateWriter.Write(residual)))
                {
                    Predicate = PredicateText.From(residual)
                });
            }
            else if (predicateInfo.HasUntranslatedPredicate)
            {
                predicateGroup.Children.Add(new PlanNodeProperty("Predicate", "(not translatable)"));
            }

            if (predicateInfo.RowGoal is { } rowGoal)
            {
                predicateGroup.Children.Add(new PlanNodeProperty("Row Goal", rowGoal.ToString("N0", CultureInfo.InvariantCulture)));
            }

            if (predicateGroup.Children.Count > 0)
            {
                result.Add(predicateGroup);
            }
        }

        if (node.SortColumns.Count > 0)
        {
            var sortGroup = new PlanNodeProperty("Order By", string.Empty);

            foreach (var sortColumn in node.SortColumns)
            {
                sortGroup.Children.Add(new PlanNodeProperty(ColumnName(sortColumn.Column), sortColumn.Ascending ? "Ascending" : "Descending")
                {
                    IsNameMonospace = true
                });
            }

            result.Add(sortGroup);
        }

        if (node.MergeInfo is { } mergeInfo)
        {
            var mergeGroup = new PlanNodeProperty("Merge", string.Empty);

            if (mergeInfo.OuterKeys.Count > 0)
            {
                mergeGroup.Children.Add(new PlanNodeProperty("Outer Keys", ColumnList(mergeInfo.OuterKeys)) { IsValueMonospace = true });
            }

            if (mergeInfo.InnerKeys.Count > 0)
            {
                mergeGroup.Children.Add(new PlanNodeProperty("Inner Keys", ColumnList(mergeInfo.InnerKeys)) { IsValueMonospace = true });
            }

            if (mergeGroup.Children.Count > 0)
            {
                result.Add(mergeGroup);
            }
        }

        if (node.GroupByColumns.Count > 0)
        {
            var aggregateGroup = new PlanNodeProperty("Aggregate", string.Empty);

            aggregateGroup.Children.Add(new PlanNodeProperty("Group By", ColumnList(node.GroupByColumns)) { IsValueMonospace = true });

            result.Add(aggregateGroup);
        }

        if (node.HashInfo is { } hashInfo)
        {
            var hashGroup = new PlanNodeProperty("Hash", string.Empty);

            if (hashInfo.BuildKeys.Count > 0)
            {
                hashGroup.Children.Add(new PlanNodeProperty("Build Keys", ColumnList(hashInfo.BuildKeys)) { IsValueMonospace = true });
            }

            if (hashInfo.ProbeKeys.Count > 0)
            {
                hashGroup.Children.Add(new PlanNodeProperty("Probe Keys", ColumnList(hashInfo.ProbeKeys)) { IsValueMonospace = true });
            }

            if (hashGroup.Children.Count > 0)
            {
                result.Add(hashGroup);
            }
        }

        if (node.IoStats is { } ioStats)
        {
            var ioGroup = new PlanNodeProperty("I/O Statistics", string.Empty) { IsExpanded = false };

            ioGroup.Children.Add(new PlanNodeProperty("Logical Reads", ioStats.LogicalReads.ToString("N0", CultureInfo.InvariantCulture)));
            ioGroup.Children.Add(new PlanNodeProperty("Physical Reads", ioStats.PhysicalReads.ToString("N0", CultureInfo.InvariantCulture)));
            ioGroup.Children.Add(new PlanNodeProperty("Read Aheads", ioStats.ReadAheads.ToString("N0", CultureInfo.InvariantCulture)));
            ioGroup.Children.Add(new PlanNodeProperty("Scans", ioStats.Scans.ToString("N0", CultureInfo.InvariantCulture)));

            if (ioStats.Rebinds > 0 || ioStats.Rewinds > 0)
            {
                ioGroup.Children.Add(new PlanNodeProperty("Rebinds", ioStats.Rebinds.ToString("N0", CultureInfo.InvariantCulture)));
                ioGroup.Children.Add(new PlanNodeProperty("Rewinds", ioStats.Rewinds.ToString("N0", CultureInfo.InvariantCulture)));
            }

            if (ioStats.LobLogicalReads > 0 || ioStats.LobPhysicalReads > 0 || ioStats.LobReadAheads > 0)
            {
                ioGroup.Children.Add(new PlanNodeProperty("Lob Logical Reads", ioStats.LobLogicalReads.ToString("N0", CultureInfo.InvariantCulture)));
                ioGroup.Children.Add(new PlanNodeProperty("Lob Physical Reads", ioStats.LobPhysicalReads.ToString("N0", CultureInfo.InvariantCulture)));
                ioGroup.Children.Add(new PlanNodeProperty("Lob Read Aheads", ioStats.LobReadAheads.ToString("N0", CultureInfo.InvariantCulture)));
            }

            result.Add(ioGroup);
        }

        if (node.OutputColumns.Count > 0)
        {
            result.Add(new PlanNodeProperty("Output Columns", string.Empty)
            {
                Items = [.. node.OutputColumns.Select(ColumnName)]
            });
        }

        if (node.DefinedValues.Count > 0)
        {
            var definedGroup = new PlanNodeProperty("Defined Values", string.Empty);

            foreach (var definedValue in node.DefinedValues)
            {
                definedGroup.Children.Add(new PlanNodeProperty(ColumnName(definedValue.Column), definedValue.Expression ?? string.Empty) { IsNameMonospace = true, IsValueMonospace = true });
            }

            result.Add(definedGroup);
        }

        return result;
    }

    private static PlanNodeProperty BoolProperty(string name, bool value)
    {
        return new PlanNodeProperty(name, value ? "True" : "False")
        {
            IsValueSuccess = value,
            IsValueError = !value
        };
    }

    private static string ColumnName(ColumnReference column)
    {
        var table = column.Table.Trim('[', ']');

        var name = column.Column.Trim('[', ']');

        return table.Length == 0 ? name : $"{table}.{name}";
    }

    private static string ObjectName(PlanNode node)
    {
        var name = string.IsNullOrEmpty(node.Schema) ? node.Table : $"{node.Schema}.{node.Table}";

        return string.IsNullOrEmpty(node.Index) ? name ?? string.Empty : $"{name}.{node.Index}";
    }

    private static string ColumnList(IEnumerable<ColumnReference> columns)
    {
        return string.Join(", ", columns.Select(c =>
        {
            var table = c.Table.Trim('[', ']');

            var column = c.Column.Trim('[', ']');

            return table.Length == 0 ? column : $"{table}.{column}";
        }));
    }
}
