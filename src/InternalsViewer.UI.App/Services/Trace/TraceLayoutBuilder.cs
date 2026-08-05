using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Models.Trace;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Media.Imaging;

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

    public required TraceBlobPalette Palette { get; init; }
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

        var palette = new TraceBlobPalette();

        var operators = TraceSourceCollector.CollectOperators(definition);

        var tabs = new List<TraceOperatorViewModel>();

        var tabsByNode = new Dictionary<int, TraceOperatorViewModel>();

        var streams = new Dictionary<int, TraceRowStreamViewModel>();

        var heldRows = new Dictionary<(int NodeId, int InputIndex), TraceHeldRowsViewModel>();

        var hashTables = new Dictionary<int, TraceHashTableViewModel>();

        var sides = new Dictionary<int, OperatorSides>();

        foreach (var op in operators)
        {
            var title = op.NodeId < 0 ? OperatorTitle(op) : $"{OperatorTitle(op)} ({op.NodeId})";

            var tab = new TraceOperatorViewModel(op.NodeId, title, OperatorDescription(op));

            ApplyHeader(tab, op, nodeFor(op.NodeId));

            tab.BlobPalette = palette;

            tabs.Add(tab);

            tabsByNode[op.NodeId] = tab;

            streams[op.NodeId] = tab.Output;
        }

        foreach (var op in operators)
        {
            var tab = tabsByNode[op.NodeId];

            if (op is JoinDefinition)
            {
                tab.IsJoinLayout = true;

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

                sides[op.NodeId] = new OperatorSides(OperatorNodeIdOf(outer),
                                                    OperatorNodeIdOf(inner),
                                                    TablesUnder(outer, visuals),
                                                    TablesUnder(inner, visuals));

                continue;
            }

            foreach (var row in InputRowsOf(op, colours, nodeFor, palette))
            {
                tab.InputRows.Add(row);
            }

            tab.MainPane = op switch
            {
                SortDefinition => HeldPane(heldRows, op.NodeId, 0),
                SelectDefinition => new TracePane(TracePaneKind.RowStream, tab.Output, "Results"),
                _ when visuals.TryGetValue(op.NodeId, out var visual) => new TracePane(TracePaneKind.Visual, visual),
                _ => TracePane.Empty
            };
        }

        if (tabsByNode.TryGetValue(definition.NodeId, out var rootTab))
        {
            rootTab.Output.IsAccumulating = true;

            if (definition is SelectDefinition)
            {
                rootTab.HasOutputPane = false;
            }
            else
            {
                rootTab.IsOutputDefaultVisible = true;
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
            InputNodes = inputNodes,
            Palette = palette
        };
    }

    private static IEnumerable<TraceInputRow> InputRowsOf(IteratorDefinition definition,
                                                          IReadOnlyDictionary<int, Color> colours,
                                                          Func<int, PlanNode?> nodeFor,
                                                          TraceBlobPalette palette)
    {
        switch (definition)
        {
            case ConcatenationDefinition concatenation:
                for (var index = 0; index < concatenation.Inputs.Count; index++)
                {
                    yield return InputRow(concatenation.Inputs[index], $"Input {index + 1}", colours, nodeFor, palette);
                }

                break;

            case SelectDefinition select:
                yield return InputRow(select.Source, "Input", colours, nodeFor, palette, hasRowCount: false);

                break;

            case SortDefinition sort:
                yield return InputRow(sort.Source, "Input", colours, nodeFor, palette, hasRowCount: false);

                break;

            case UnaryDefinition unary:
                yield return InputRow(unary.Source, "Input", colours, nodeFor, palette);

                break;
        }
    }

    private static TraceInputRow InputRow(IteratorDefinition input,
                                          string label,
                                          IReadOnlyDictionary<int, Color> colours,
                                          Func<int, PlanNode?> nodeFor,
                                          TraceBlobPalette palette,
                                          bool hasRowCount = true)
    {
        var node = nodeFor(input.NodeId);

        var physical = node?.PhysicalOperator is { Length: > 0 } name ? name : DisplayName(input);

        var heading = input.NodeId < 0 ? physical : $"{physical} ({input.NodeId})";

        return new TraceInputRow(input.NodeId, heading)
        {
            Label = label,
            Blob = Accent(colours, input.NodeId) is { } accent ? palette.For(input.NodeId, accent) : null,
            Icon = node is null ? null : new SvgImageSource(PlanIconResolver.Resolve(node)),
            HasRowCount = hasRowCount
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

            case ConcatenationDefinition concatenation:
                foreach (var input in concatenation.Inputs)
                {
                    Index(input, definitions, depths, ordered, depth + 1);
                }

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
            ConcatenationDefinition => "Concatenation",
            SortDefinition => "Sort",
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

        if (definition is SortDefinition sort)
        {
            var orderBy = string.Join(", ", sort.Keys.Select(k => k.Descending ? $"{k.Column} DESC" : k.Column));

            var prefix = sort.TopCount is { } topCount
                ? $"TOP {topCount:N0} "
                : sort.IsDistinct ? "DISTINCT " : string.Empty;

            return $"{prefix}ORDER BY {orderBy}";
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
            ConcatenationDefinition => ("Input 1", "Input 2"),
            SortDefinition => ("Input", string.Empty),
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
            SortDefinition sort => (sort.Source, null),
            ConcatenationDefinition concatenation
                => (concatenation.Inputs.Count > 0 ? concatenation.Inputs[0] : null,
                    concatenation.Inputs.Count > 1 ? concatenation.Inputs[1] : null),
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
            var logical = node?.LogicalOperator ?? string.Empty;

            return new TracePane(TracePaneKind.RowStream, tab.Output, label)
            {
                SourceNodeId = input.NodeId,
                AccentColour = Accent(colours, input.NodeId),
                Icon = icon,
                Heading = heading,
                Subheading = logical.Length > 0 && logical != physical ? logical : string.Empty
            };
        }

        if (visuals.TryGetValue(input.NodeId, out var visual))
        {
            return new TracePane(TracePaneKind.Visual, visual, label)
            {
                SourceNodeId = input.NodeId,
                AccentColour = Accent(colours, input.NodeId),
                Icon = icon,
                Heading = heading,
                Subheading = ObjectName(visual)
            };
        }

        return TracePane.Empty;
    }

    private static void ApplyHeader(TraceOperatorViewModel tab, IteratorDefinition definition, PlanNode? node)
    {
        if (node is null && definition is SelectDefinition)
        {
            node = new PlanNode { PhysicalOperator = "SELECT", IsStatement = true };
        }

        var physical = node?.PhysicalOperator is { Length: > 0 } name ? name : DisplayName(definition);

        tab.Heading = definition.NodeId < 0 ? physical : $"{physical} ({definition.NodeId})";

        tab.Icon = node is null ? null : PlanIconResolver.Resolve(node);

        var logical = node?.LogicalOperator ?? string.Empty;

        tab.Subheading = logical.Length > 0 && logical != physical ? logical : string.Empty;

        tab.JoinRule = (definition as JoinDefinition)?.JoinType.Decide(true, true);
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
