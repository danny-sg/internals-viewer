using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Locators;
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

        Pulls = ExpressionPullResolver.Resolve(statement);
    }

    public PlanNode Statement { get; }

    private Dictionary<int, PlanNode> Nodes { get; } = [];

    private Dictionary<int, PlanNode> Parents { get; } = [];

    private Dictionary<int, int> Levels { get; } = [];

    private List<ExpressionPull> Pulls { get; }

    public static IEnumerable<string> GetDefinitions(PlanNode node)
        => node.DefinedValues.Select(d => $"{string.Join(", ", d.Columns.Select(c => c.Column))} = {GetText(d)}");

    public static string GetText(DefinedValueInfo value)
        => value.ParsedExpression is { } parsed ? PredicateText.From(parsed).ToString() : value.Expression ?? string.Empty;

    public static string GetObjectName(PlanNode node)
        => string.IsNullOrEmpty(node.Table)
            ? string.Empty
            : string.Join(".", new[] { node.Schema, node.Table, node.Index }.Where(p => !string.IsNullOrEmpty(p)));

    public int? GetLevel(int nodeId) => Levels.TryGetValue(nodeId, out var level) ? level : null;

    public PlanNode? GetNode(int nodeId) => Nodes.GetValueOrDefault(nodeId);

    public DefinedValueInfo? FindDefinition(string column)
        => Nodes.Values
                .Where(OperatorClassifier.IsComputeScalar)
                .SelectMany(n => n.DefinedValues)
                .FirstOrDefault(d => d.Expression is { Length: > 0 } && d.Columns.Exists(c => c.Column == column));

    public PlanNode? GetPuller(PlanNode computeScalar)
        => (Pulls.Find(p => ReferenceEquals(p.ComputeScalar, computeScalar) && p.OutputValues.Count > 0)
            ?? Pulls.Find(p => ReferenceEquals(p.ComputeScalar, computeScalar)))?.Puller;

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

            SqlTypeInfo? type = inputs switch
            {
                [var input] when value is null => input.Column.Type,
                [var first, ..] => first.Column.Type with { MaxLength = 0 },
                _ => null
            };

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
