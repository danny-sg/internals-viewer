using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.Services.Query.Trace;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

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

        var tabsByNode = CreateTabs(operators, palette, nodeFor);

        var heldRows = new Dictionary<(int NodeId, int InputIndex), TraceHeldRowsViewModel>();

        var hashTables = new Dictionary<int, TraceHashTableViewModel>();

        var sides = new Dictionary<int, OperatorSides>();

        var aggregates = new Dictionary<int, TraceAggregateViewModel>();

        WirePanes(operators, tabsByNode, visuals, colours, nodeFor, palette, heldRows, hashTables, sides, aggregates);

        ConfigureRootTab(definition, tabsByNode);

        var sourceVisuals = MapSourceVisuals(definition, visuals);

        var nodes = BuildContexts(ordered,
                                  definitions,
                                  depths,
                                  colours,
                                  tabsByNode,
                                  visuals,
                                  sourceVisuals,
                                  hashTables,
                                  heldRows,
                                  sides,
                                  aggregates);

        return new TraceLayout
        {
            Tabs = [.. operators.Select(o => tabsByNode[o.NodeId])],
            Nodes = nodes,
            Palette = palette
        };
    }

    private static Dictionary<int, TraceOperatorViewModel> CreateTabs(IReadOnlyList<IteratorDefinition> operators,
                                                                      TraceBlobPalette palette,
                                                                      Func<int, PlanNode?> nodeFor)
    {
        var tabsByNode = new Dictionary<int, TraceOperatorViewModel>();

        foreach (var op in operators)
        {
            var title = op.NodeId < 0 ? OperatorTitle(op) : $"{OperatorTitle(op)} ({op.NodeId})";

            var tab = new TraceOperatorViewModel(op.NodeId, title, OperatorDescription(op));

            ApplyHeader(tab, op, nodeFor(op.NodeId));

            tab.BlobPalette = palette;

            tabsByNode[op.NodeId] = tab;
        }

        return tabsByNode;
    }

    private static void WirePanes(IReadOnlyList<IteratorDefinition> operators,
                                  IReadOnlyDictionary<int, TraceOperatorViewModel> tabsByNode,
                                  IReadOnlyDictionary<int, TraceVisualViewModel> visuals,
                                  IReadOnlyDictionary<int, Color> colours,
                                  Func<int, PlanNode?> nodeFor,
                                  TraceBlobPalette palette,
                                  Dictionary<(int NodeId, int InputIndex), TraceHeldRowsViewModel> heldRows,
                                  Dictionary<int, TraceHashTableViewModel> hashTables,
                                  Dictionary<int, OperatorSides> sides,
                                  Dictionary<int, TraceAggregateViewModel> aggregates)
    {
        foreach (var op in operators)
        {
            var tab = tabsByNode[op.NodeId];

            if (op is JoinDefinition)
            {
                tab.IsJoinLayout = true;

                var (outer, inner) = DefinitionTreeWalker.Inputs(op);

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
                StreamAggregateDefinition aggregate => AggregatePane(aggregates, aggregate),
                HashAggregateDefinition => HashAggregatePane(hashTables, op.NodeId),
                _ when visuals.TryGetValue(op.NodeId, out var visual) => new TracePane(TracePaneKind.Visual, visual),
                _ => TracePane.Empty
            };
        }
    }

    private static void ConfigureRootTab(IteratorDefinition definition, IReadOnlyDictionary<int, TraceOperatorViewModel> tabsByNode)
    {
        if (!tabsByNode.TryGetValue(definition.NodeId, out var rootTab))
        {
            return;
        }

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

    private static Dictionary<int, TraceVisualViewModel> MapSourceVisuals(IteratorDefinition definition,
                                                                          IReadOnlyDictionary<int, TraceVisualViewModel> visuals)
    {
        var sourceVisuals = new Dictionary<int, TraceVisualViewModel>();

        foreach (var source in TraceSourceCollector.Collect(definition))
        {
            if (!sourceVisuals.ContainsKey(source.OperatorNodeId) && visuals.TryGetValue(source.NodeId, out var visual))
            {
                sourceVisuals[source.OperatorNodeId] = visual;
            }
        }

        return sourceVisuals;
    }

    private static Dictionary<int, TraceNodeContext> BuildContexts(IReadOnlyList<int> ordered,
                                                                   IReadOnlyDictionary<int, IteratorDefinition> definitions,
                                                                   IReadOnlyDictionary<int, int> depths,
                                                                   IReadOnlyDictionary<int, Color> colours,
                                                                   IReadOnlyDictionary<int, TraceOperatorViewModel> tabsByNode,
                                                                   IReadOnlyDictionary<int, TraceVisualViewModel> visuals,
                                                                   IReadOnlyDictionary<int, TraceVisualViewModel> sourceVisuals,
                                                                   IReadOnlyDictionary<int, TraceHashTableViewModel> hashTables,
                                                                   IReadOnlyDictionary<(int NodeId, int InputIndex), TraceHeldRowsViewModel> heldRows,
                                                                   IReadOnlyDictionary<int, OperatorSides> sides,
                                                                   IReadOnlyDictionary<int, TraceAggregateViewModel> aggregates)
    {
        var nodes = new Dictionary<int, TraceNodeContext>();

        foreach (var nodeId in ordered)
        {
            var (outer, inner) = DefinitionTreeWalker.Inputs(definitions[nodeId]);

            var held = heldRows.Where(h => h.Key.NodeId == nodeId)
                               .ToDictionary(h => h.Key.InputIndex, h => h.Value);

            nodes[nodeId] = new TraceNodeContext
            {
                Definition = definitions[nodeId],
                Depth = depths[nodeId],
                Colour = colours[nodeId],
                InputNodes = (outer?.NodeId ?? -1, inner?.NodeId ?? -1),
                Tab = tabsByNode.GetValueOrDefault(nodeId),
                Visual = visuals.GetValueOrDefault(nodeId),
                SourceVisual = sourceVisuals.GetValueOrDefault(nodeId),
                HashTable = hashTables.GetValueOrDefault(nodeId),
                Aggregates = aggregates.GetValueOrDefault(nodeId),
                HeldRows = held,
                Sides = sides.GetValueOrDefault(nodeId)
            };
        }

        return nodes;
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
        var header = OperatorHeader.For(input, nodeFor(input.NodeId));

        return new TraceInputRow(input.NodeId, header.Heading)
        {
            Label = label,
            Blob = Accent(colours, input.NodeId) is { } accent ? palette.For(input.NodeId, accent) : null,
            Icon = header.Icon is { } icon ? new SvgImageSource(icon) : null,
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

            var colour = ColourHelpers.HsvToColor(hue, 150, 220);

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

        foreach (var child in DefinitionTreeWalker.ChildrenOf(definition))
        {
            Index(child, definitions, depths, ordered, depth + 1);
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
            StreamAggregateDefinition => "Stream Aggregate",
            HashAggregateDefinition => "Hash Match",
            ComputeScalarDefinition => "Compute Scalar",
            FilterDefinition => "Filter",
            SegmentDefinition => "Segment",
            SequenceProjectDefinition => "Sequence Project",
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

        if (definition is StreamAggregateDefinition aggregate)
        {
            var aggregates = string.Join(", ", aggregate.Aggregates.Select(a => a.ToText()));

            return aggregate.IsScalar ? aggregates : $"{aggregates} GROUP BY {string.Join(", ", aggregate.GroupBy)}".TrimStart();
        }

        if (definition is HashAggregateDefinition hashAggregate)
        {
            var hashed = string.Join(", ", hashAggregate.Aggregates.Select(a => a.ToText()));

            return $"{hashed} GROUP BY {string.Join(", ", hashAggregate.GroupBy)}".TrimStart();
        }

        if (definition is ComputeScalarDefinition compute)
        {
            return string.Join(", ", compute.Columns.Select(c => c.Name));
        }

        if (definition is FilterDefinition filter)
        {
            return filter.Residual is null ? string.Empty : PredicateText.From(filter.Residual).ToString();
        }

        if (definition is SegmentDefinition segment)
        {
            return segment.GroupBy.Count == 0 ? "one segment" : $"GROUP BY {string.Join(", ", segment.GroupBy)}";
        }

        if (definition is SequenceProjectDefinition sequence)
        {
            return string.Join(", ", sequence.Columns.Select(c => c.ToText()));
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
            StreamAggregateDefinition => ("Input", string.Empty),
            HashAggregateDefinition => ("Input", string.Empty),
            ComputeScalarDefinition => ("Input", string.Empty),
            FilterDefinition => ("Input", string.Empty),
            SegmentDefinition => ("Input", string.Empty),
            SequenceProjectDefinition => ("Input", string.Empty),
            _ => ("Outer Input", "Inner Input")
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

        var header = OperatorHeader.For(input, nodeFor(input.NodeId));

        if (tabsByNode.TryGetValue(input.NodeId, out var tab))
        {
            return new TracePane(TracePaneKind.RowStream, tab.Output, label)
            {
                SourceNodeId = input.NodeId,
                AccentColour = Accent(colours, input.NodeId),
                Icon = header.Icon,
                Heading = header.Heading,
                Subheading = header.Subheading
            };
        }

        if (visuals.TryGetValue(input.NodeId, out var visual))
        {
            return new TracePane(TracePaneKind.Visual, visual, label)
            {
                SourceNodeId = input.NodeId,
                AccentColour = Accent(colours, input.NodeId),
                Icon = header.Icon,
                Heading = header.Heading,
                Subheading = visual.AllocationUnit.DisplayName()
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

        var header = OperatorHeader.For(definition, node);

        tab.Heading = header.Heading;

        tab.Icon = header.Icon;

        tab.Subheading = header.Subheading;

        tab.JoinRule = (definition as JoinDefinition)?.JoinType.Decide(true, true);
    }

    private static Windows.UI.Color? Accent(IReadOnlyDictionary<int, Color> colours, int nodeId)
        => colours.TryGetValue(nodeId, out var colour) ? colour.ToWindowsColor() : null;

    private static TracePane HashAggregatePane(Dictionary<int, TraceHashTableViewModel> hashTables, int nodeId)
    {
        var hashTable = new TraceHashTableViewModel(RecordColumnFilter.All);

        hashTables[nodeId] = hashTable;

        return new TracePane(TracePaneKind.HashTable, hashTable, "Hash Table");
    }

    private static TracePane AggregatePane(Dictionary<int, TraceAggregateViewModel> aggregates, StreamAggregateDefinition definition)
    {
        var viewModel = new TraceAggregateViewModel(definition.Aggregates, definition.GroupBy);

        aggregates[definition.NodeId] = viewModel;

        return new TracePane(TracePaneKind.Aggregates, viewModel, "Aggregates");
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
