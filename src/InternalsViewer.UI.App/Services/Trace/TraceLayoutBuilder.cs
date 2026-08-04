using System;
using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Trace;
using InternalsViewer.UI.App.ViewModels.Query.Trace;

namespace InternalsViewer.UI.App.Services.Trace;

public sealed class TraceLayout
{
    public required IReadOnlyList<TraceOperatorViewModel> Tabs { get; init; }

    public required IReadOnlyDictionary<int, TraceRowStreamViewModel> Streams { get; init; }

    public required IReadOnlyDictionary<(int NodeId, int InputIndex), TraceHeldRowsViewModel> HeldRows { get; init; }

    public required IReadOnlyDictionary<int, TraceHashTableViewModel> HashTables { get; init; }

    public required IReadOnlyDictionary<int, IteratorDefinition> Definitions { get; init; }

    public required IReadOnlyDictionary<int, OperatorSides> Sides { get; init; }

    public required IReadOnlyDictionary<int, TraceVisualViewModel> VisualByOperator { get; init; }
}

public static class TraceLayoutBuilder
{
    public static TraceLayout Build(IteratorDefinition definition,
                                    IReadOnlyDictionary<int, TraceVisualViewModel> visuals,
                                    Func<int, PlanNode?> nodeFor)
    {
        var definitions = new Dictionary<int, IteratorDefinition>();

        Index(definition, definitions);

        var operators = TraceSourceCollector.CollectOperators(definition);

        var tabs = new List<TraceOperatorViewModel>();

        var tabsByNode = new Dictionary<int, TraceOperatorViewModel>();

        var streams = new Dictionary<int, TraceRowStreamViewModel>();

        var heldRows = new Dictionary<(int NodeId, int InputIndex), TraceHeldRowsViewModel>();

        var hashTables = new Dictionary<int, TraceHashTableViewModel>();

        var sides = new Dictionary<int, OperatorSides>();

        if (operators.Count == 0)
        {
            var visual = visuals[definition.NodeId];

            var leafTab = new TraceOperatorViewModel(definition.NodeId, visual.Title, string.Empty)
            {
                OuterTop = new TracePane(TracePaneKind.Visual, visual, visual.Title)
            };

            tabs.Add(leafTab);

            streams[definition.NodeId] = leafTab.Output;
        }

        foreach (var op in operators)
        {
            var tab = new TraceOperatorViewModel(op.NodeId, OperatorTitle(op), OperatorDescription(op));

            tabs.Add(tab);

            tabsByNode[op.NodeId] = tab;

            streams[op.NodeId] = tab.Output;
        }

        foreach (var op in operators)
        {
            var tab = tabsByNode[op.NodeId];

            var (outer, inner) = Inputs(op);

            tab.OuterTop = InputPane(outer, tabsByNode, visuals);
            tab.InnerTop = InputPane(inner, tabsByNode, visuals);

            switch (op)
            {
                case HashMatchDefinition hash:
                    var hashTable = new TraceHashTableViewModel(HashTableFilter(hash.Build.Source, nodeFor));

                    hashTables[op.NodeId] = hashTable;

                    tab.OuterBottom = new TracePane(TracePaneKind.HashTable, hashTable);
                    tab.InnerBottom = HeldPane(heldRows, op.NodeId, 1);
                    break;

                case MergeJoinDefinition or NestedLoopsDefinition:
                    tab.OuterBottom = HeldPane(heldRows, op.NodeId, 0);
                    tab.InnerBottom = HeldPane(heldRows, op.NodeId, 1);
                    break;
            }

            if (op is JoinDefinition)
            {
                sides[op.NodeId] = new OperatorSides(OperatorNodeIdOf(outer),
                                                    OperatorNodeIdOf(inner),
                                                    TablesUnder(outer, visuals),
                                                    TablesUnder(inner, visuals));
            }
        }

        var visualByOperator = new Dictionary<int, TraceVisualViewModel>();

        foreach (var source in TraceSourceCollector.Collect(definition))
        {
            if (!visualByOperator.ContainsKey(source.OperatorNodeId) && visuals.TryGetValue(source.NodeId, out var visual))
            {
                visualByOperator[source.OperatorNodeId] = visual;
            }
        }

        if (operators.Count == 0 && visuals.TryGetValue(definition.NodeId, out var rootVisual))
        {
            visualByOperator[definition.NodeId] = rootVisual;
        }

        return new TraceLayout
        {
            Tabs = tabs,
            Streams = streams,
            HeldRows = heldRows,
            HashTables = hashTables,
            Definitions = definitions,
            Sides = sides,
            VisualByOperator = visualByOperator
        };
    }

    private static void Index(IteratorDefinition definition, Dictionary<int, IteratorDefinition> definitions)
    {
        definitions[definition.NodeId] = definition;

        switch (definition)
        {
            case NestedLoopsDefinition loops:
                Index(loops.Outer, definitions);
                Index(loops.Inner, definitions);
                break;

            case MergeJoinDefinition merge:
                Index(merge.Outer.Source, definitions);
                Index(merge.Inner.Source, definitions);
                break;

            case HashMatchDefinition hash:
                Index(hash.Build.Source, definitions);
                Index(hash.Probe.Source, definitions);
                break;

            case UnaryDefinition unary:
                Index(unary.Source, definitions);
                break;
        }
    }

    private static string OperatorTitle(IteratorDefinition definition)
        => definition switch
        {
            HashMatchDefinition => "Hash Match",
            MergeJoinDefinition => "Merge Join",
            NestedLoopsDefinition => "Nested Loops",
            TopDefinition => "Top",
            _ => "Results"
        };

    /// <summary>
    /// The join type and the columns matched on, stated the way the operator states them
    /// </summary>
    private static string OperatorDescription(IteratorDefinition definition)
    {
        if (definition is TopDefinition top)
        {
            return $"TOP {top.RowCount:N0}";
        }

        if (definition is not JoinDefinition join)
        {
            return string.Empty;
        }

        var keys = definition switch
        {
            HashMatchDefinition hash => hash.Build.JoinColumns,
            MergeJoinDefinition merge => merge.Outer.JoinColumns,
            _ => []
        };

        var on = keys.Count > 0 ? $" on {string.Join(", ", keys)}" : string.Empty;

        var residual = join.Residual is null ? string.Empty : " with residual";

        return $"{join.JoinType.ToDisplayName()}{on}{residual}";
    }

    private static (IteratorDefinition? Outer, IteratorDefinition? Inner) Inputs(IteratorDefinition definition)
        => definition switch
        {
            NestedLoopsDefinition loops => (loops.Outer, loops.Inner),
            MergeJoinDefinition merge => (merge.Outer.Source, merge.Inner.Source),
            HashMatchDefinition hash => (hash.Build.Source, hash.Probe.Source),
            TopDefinition top => (top.Source, null),
            _ => (null, null)
        };

    private static TracePane InputPane(IteratorDefinition? input,
                                       IReadOnlyDictionary<int, TraceOperatorViewModel> tabsByNode,
                                       IReadOnlyDictionary<int, TraceVisualViewModel> visuals)
    {
        if (input is null)
        {
            return TracePane.Empty;
        }

        if (tabsByNode.TryGetValue(input.NodeId, out var tab))
        {
            return new TracePane(TracePaneKind.RowStream, tab.Output, tab.Title);
        }

        if (visuals.TryGetValue(input.NodeId, out var visual))
        {
            return new TracePane(TracePaneKind.Visual, visual, visual.Title);
        }

        return TracePane.Empty;
    }

    private static TracePane HeldPane(Dictionary<(int NodeId, int InputIndex), TraceHeldRowsViewModel> heldRows,
                                      int nodeId,
                                      int inputIndex)
    {
        var held = new TraceHeldRowsViewModel();

        heldRows[(nodeId, inputIndex)] = held;

        return new TracePane(TracePaneKind.HeldRows, held);
    }

    private static RecordColumnFilter HashTableFilter(IteratorDefinition build, Func<int, PlanNode?> nodeFor)
        => RecordColumnFilter.For(nodeFor(Unwrap(build).NodeId));

    private static int OperatorNodeIdOf(IteratorDefinition? side)
        => side is not null && Unwrap(side) is JoinDefinition join ? join.NodeId : -1;

    /// <summary>
    /// The objects read anywhere below one side of a join, which is what says whose column an operator is asked for
    /// </summary>
    private static HashSet<string> TablesUnder(IteratorDefinition? side, IReadOnlyDictionary<int, TraceVisualViewModel> visuals)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (side is null)
        {
            return tables;
        }

        foreach (var source in TraceSourceCollector.Collect(side))
        {
            if (visuals.TryGetValue(source.NodeId, out var visual) && visual.AllocationUnit.TableName is { } table)
            {
                tables.Add(table);
            }
        }

        return tables;
    }

    private static IteratorDefinition Unwrap(IteratorDefinition definition)
        => definition is UnaryDefinition unary ? Unwrap(unary.Source) : definition;
}
