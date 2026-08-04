using System;
using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Plan;
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

    public required IReadOnlyDictionary<int, int> Depths { get; init; }

    public required IReadOnlyDictionary<int, Color> Colours { get; init; }

    public required IReadOnlyDictionary<int, (int Outer, int Inner)> InputNodes { get; init; }
}

public static class TraceLayoutBuilder
{
    public static TraceLayout Build(IteratorDefinition definition,
                                    IReadOnlyDictionary<int, TraceVisualViewModel> visuals,
                                    Func<int, PlanNode?> nodeFor)
    {
        var definitions = new Dictionary<int, IteratorDefinition>();

        var depths = new Dictionary<int, int>();

        var ordered = new List<int>();

        Index(definition, definitions, depths, ordered, 0);

        var colours = BuildColours(ordered, visuals);

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

            var leafNode = nodeFor(definition.NodeId);

            var leafTab = new TraceOperatorViewModel(definition.NodeId, visual.Title, string.Empty)
            {
                OuterTop = new TracePane(TracePaneKind.Visual, visual)
                {
                    AccentColour = Accent(colours, definition.NodeId),
                    Icon = leafNode is null ? null : PlanIconResolver.Resolve(leafNode),
                    Heading = visual.Title
                }
            };

            tabs.Add(leafTab);

            streams[definition.NodeId] = leafTab.Output;
        }

        foreach (var op in operators)
        {
            var title = op.NodeId < 0 ? OperatorTitle(op) : $"{OperatorTitle(op)} ({op.NodeId})";

            var tab = new TraceOperatorViewModel(op.NodeId, title, OperatorDescription(op));

            tabs.Add(tab);

            tabsByNode[op.NodeId] = tab;

            streams[op.NodeId] = tab.Output;
        }

        foreach (var op in operators)
        {
            var tab = tabsByNode[op.NodeId];

            var (outer, inner) = Inputs(op);

            var (outerLabel, innerLabel) = InputLabels(op);

            tab.OuterTop = InputPane(outer, outerLabel, tabsByNode, visuals, colours, nodeFor);
            tab.InnerTop = InputPane(inner, innerLabel, tabsByNode, visuals, colours, nodeFor);

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

        var inputNodes = new Dictionary<int, (int Outer, int Inner)>();

        foreach (var op in operators)
        {
            var (outerInput, innerInput) = Inputs(op);

            inputNodes[op.NodeId] = (outerInput?.NodeId ?? -1, innerInput?.NodeId ?? -1);
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
            VisualByOperator = visualByOperator,
            Depths = depths,
            Colours = colours,
            InputNodes = inputNodes
        };
    }

    private static Dictionary<int, Color> BuildColours(IReadOnlyList<int> ordered,
                                                       IReadOnlyDictionary<int, TraceVisualViewModel> visuals)
    {
        var colours = new Dictionary<int, Color>();

        var used = new HashSet<int>();

        var paletteIndex = 0;

        foreach (var nodeId in ordered)
        {
            var colour = visuals.TryGetValue(nodeId, out var visual) && used.Add(visual.ObjectColour.ToArgb())
                ? visual.ObjectColour
                : NextColour(used, ref paletteIndex);

            colours[nodeId] = colour;
        }

        return colours;
    }

    private static Color NextColour(HashSet<int> used, ref int paletteIndex)
    {
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var hue = 24 + ((paletteIndex * 79) % 208);

            paletteIndex++;

            var colour = Helpers.ColourHelpers.HsvToColor(hue, 150, 220);

            if (used.Add(colour.ToArgb()))
            {
                return colour;
            }
        }

        return Color.Gray;
    }

    private static void Index(IteratorDefinition definition,
                              Dictionary<int, IteratorDefinition> definitions,
                              Dictionary<int, int> depths,
                              List<int> ordered,
                              int depth)
    {
        definitions[definition.NodeId] = definition;

        depths[definition.NodeId] = depth;

        ordered.Add(definition.NodeId);

        switch (definition)
        {
            case NestedLoopsDefinition loops:
                Index(loops.Outer, definitions, depths, ordered, depth + 1);
                Index(loops.Inner, definitions, depths, ordered, depth + 1);
                break;

            case MergeJoinDefinition merge:
                Index(merge.Outer.Source, definitions, depths, ordered, depth + 1);
                Index(merge.Inner.Source, definitions, depths, ordered, depth + 1);
                break;

            case HashMatchDefinition hash:
                Index(hash.Build.Source, definitions, depths, ordered, depth + 1);
                Index(hash.Probe.Source, definitions, depths, ordered, depth + 1);
                break;

            case UnaryDefinition unary:
                Index(unary.Source, definitions, depths, ordered, depth + 1);
                break;
        }
    }

    public static string DisplayName(IteratorDefinition definition)
        => definition switch
        {
            HashMatchDefinition => "Hash Match",
            MergeJoinDefinition => "Merge Join",
            NestedLoopsDefinition => "Nested Loops",
            TopDefinition => "Top",
            SelectDefinition => "SELECT",
            SeekDefinition => "Index Seek",
            RangeDefinition => "Index Scan",
            HeapFetchDefinition => "RID Lookup",
            AllocationScanDefinition => "Table Scan",
            _ => "Operator"
        };

    private static string OperatorTitle(IteratorDefinition definition)
        => DisplayName(definition);

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

    private static (string Outer, string Inner) InputLabels(IteratorDefinition definition)
        => definition switch
        {
            HashMatchDefinition => ("Build Input", "Probe Input"),
            TopDefinition => ("Input", string.Empty),
            SelectDefinition => ("Input", string.Empty),
            _ => ("Outer Input", "Inner Input")
        };

    private static (IteratorDefinition? Outer, IteratorDefinition? Inner) Inputs(IteratorDefinition definition)
        => definition switch
        {
            NestedLoopsDefinition loops => (loops.Outer, loops.Inner),
            MergeJoinDefinition merge => (merge.Outer.Source, merge.Inner.Source),
            HashMatchDefinition hash => (hash.Build.Source, hash.Probe.Source),
            TopDefinition top => (top.Source, null),
            SelectDefinition select => (select.Source, null),
            _ => (null, null)
        };

    private static TracePane InputPane(IteratorDefinition? input,
                                       string label,
                                       IReadOnlyDictionary<int, TraceOperatorViewModel> tabsByNode,
                                       IReadOnlyDictionary<int, TraceVisualViewModel> visuals,
                                       IReadOnlyDictionary<int, Color> colours,
                                       Func<int, PlanNode?> nodeFor)
    {
        if (input is null)
        {
            return TracePane.Empty;
        }

        var node = nodeFor(input.NodeId);

        var physical = node?.PhysicalOperator is { Length: > 0 } name ? name : DisplayName(input);

        var heading = input.NodeId < 0 ? physical : $"{physical} ({input.NodeId})";

        var icon = node is null ? null : PlanIconResolver.Resolve(node);

        if (tabsByNode.TryGetValue(input.NodeId, out var tab))
        {
            var joinRule = (input as JoinDefinition)?.JoinType.Decide(true, true);

            var logical = node?.LogicalOperator ?? string.Empty;

            return new TracePane(TracePaneKind.RowStream, tab.Output, label)
            {
                AccentColour = Accent(colours, input.NodeId),
                Icon = icon,
                Heading = heading,
                Subheading = joinRule is null && logical.Length > 0 && logical != physical ? logical : string.Empty,
                JoinRule = joinRule
            };
        }

        if (visuals.TryGetValue(input.NodeId, out var visual))
        {
            return new TracePane(TracePaneKind.Visual, visual, label)
            {
                AccentColour = Accent(colours, input.NodeId),
                Icon = icon,
                Heading = heading,
                Subheading = ObjectName(visual)
            };
        }

        return TracePane.Empty;
    }

    private static Windows.UI.Color? Accent(IReadOnlyDictionary<int, Color> colours, int nodeId)
        => colours.TryGetValue(nodeId, out var colour)
            ? Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B)
            : null;

    private static string ObjectName(TraceVisualViewModel visual)
        => string.IsNullOrEmpty(visual.AllocationUnit.IndexName)
            ? visual.AllocationUnit.TableName ?? string.Empty
            : visual.AllocationUnit.IndexName;

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
