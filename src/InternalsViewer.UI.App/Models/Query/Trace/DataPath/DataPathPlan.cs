using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.UI.App.Models.Query.Trace.DataPath;

public sealed class DataPathPlan
{
    public DataPathPlan(PlanNode statement)
    {
        Statement = statement;

        foreach (var child in statement.Children)
        {
            Add(child, null, 0);
        }
    }

    public PlanNode Statement { get; }

    private Dictionary<int, PlanNode> Nodes { get; } = [];

    private Dictionary<int, PlanNode> Parents { get; } = [];

    private Dictionary<int, int> Levels { get; } = [];

    public static string GetLabel(PlanNode node) => $"{node.PhysicalOperator} (Node {node.NodeId})";

    public int? GetLevel(int nodeId) => Levels.TryGetValue(nodeId, out var level) ? level : null;

    public List<int> GetTops(Func<int, bool> isOperator)
    {
        var tops = new List<int>();

        foreach (var child in Statement.Children)
        {
            AddTops(child, isOperator, tops);
        }

        return tops;
    }

    public List<DataPathColumn> GetColumns(Func<int, bool> isOperator, Func<int, int, PlanOutputColumn?> getColumn)
    {
        var columns = new List<DataPathColumn>();

        if (Statement.Children is not [var root, ..])
        {
            return columns;
        }

        foreach (var output in root.OutputColumns)
        {
            var inputs = new List<DataPathInput>();

            var (expression, value) = Resolve(root, output.Column, output.Table, isOperator, getColumn, inputs);

            var type = value is not null
                ? value.DataType?.ToString().ToLowerInvariant() ?? string.Empty
                : inputs is [var input] ? input.Column.Type.ToString() : string.Empty;

            columns.Add(new DataPathColumn(output.Column, type, inputs, expression, value));
        }

        return columns;
    }

    public PlanNode? GetExpression(int? consumerNodeId, int inputNodeId)
    {
        for (var current = Parents.GetValueOrDefault(inputNodeId);
             current is not null && current.NodeId != consumerNodeId;
             current = Parents.GetValueOrDefault(current.NodeId))
        {
            if (current.DefinedValues.Count > 0)
            {
                return current;
            }
        }

        return null;
    }

    public bool IsBlockingInput(int consumerNodeId, int inputNodeId)
    {
        for (var current = Nodes.GetValueOrDefault(inputNodeId); current is not null; current = Parents.GetValueOrDefault(current.NodeId))
        {
            if (Parents.GetValueOrDefault(current.NodeId) is { } parent && parent.NodeId == consumerNodeId)
            {
                return OperatorClassifier.RoleOf(parent, current) == InputRole.Blocking;
            }
        }

        return false;
    }

    private static void AddTops(PlanNode node, Func<int, bool> isOperator, List<int> tops)
    {
        if (isOperator(node.NodeId))
        {
            tops.Add(node.NodeId);

            return;
        }

        foreach (var child in node.Children)
        {
            AddTops(child, isOperator, tops);
        }
    }

    private static (PlanNode? Expression, DefinedValueInfo? Value) Resolve(PlanNode node,
                                                                           string column,
                                                                           string table,
                                                                           Func<int, bool> isOperator,
                                                                           Func<int, int, PlanOutputColumn?> getColumn,
                                                                           List<DataPathInput> inputs)
    {
        if (isOperator(node.NodeId))
        {
            var index = node.OutputColumns.FindIndex(c => IsMatch(c, column, table));

            if (index >= 0 && getColumn(node.NodeId, index) is { } found)
            {
                inputs.Add(new DataPathInput(node.NodeId, found));
            }

            return (null, null);
        }

        if (node.DefinedValues.Find(d => d.Columns.Exists(c => IsMatch(c, column, table))) is { } value)
        {
            foreach (var reference in value.References)
            {
                if (node.Children.Find(c => c.OutputColumns.Exists(o => o.Column == reference)) is { } child)
                {
                    Resolve(child, reference, string.Empty, isOperator, getColumn, inputs);
                }
            }

            return (node, value);
        }

        return node.Children.Find(c => c.OutputColumns.Exists(o => IsMatch(o, column, table))) is { } source
            ? Resolve(source, column, table, isOperator, getColumn, inputs)
            : (null, null);
    }

    private static bool IsMatch(ColumnReference reference, string column, string table)
        => reference.Column == column && (table.Length == 0 || reference.Table.Length == 0 || reference.Table == table);

    private void Add(PlanNode node, PlanNode? parent, int level)
    {
        Nodes.TryAdd(node.NodeId, node);

        Levels.TryAdd(node.NodeId, level);

        if (parent is not null)
        {
            Parents.TryAdd(node.NodeId, parent);
        }

        foreach (var child in node.Children)
        {
            Add(child, node, level + 1);
        }
    }
}
