using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Simulation;
using InternalsViewer.UI.App.Services.Query.Simulation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.ViewModels.Query.Simulation;

public static class SimulationLayoutBuilder
{
    public static SimulationLayout Build(IteratorDefinition definition,
                                    IReadOnlyDictionary<int, SimulationVisualViewModel> visuals,
                                    Func<int, PlanNode?> nodeFor)
    {
        var definitions = new Dictionary<int, IteratorDefinition>();

        var depths = new Dictionary<int, int>();

        var ordered = new List<int>();

        Index(definition, definitions, depths, ordered, 0);

        var colours = BuildColours(ordered, visuals);

        var palette = new SimulationBlobPalette();

        var operators = SimulationSourceCollector.CollectOperators(definition);

        var tabsByNode = CreateTabs(operators, palette, nodeFor);

        var heldRows = new Dictionary<(int NodeId, int InputIndex), SimulationHeldRowsViewModel>();

        var hashTables = new Dictionary<int, SimulationHashTableViewModel>();

        var localHashTables = new Dictionary<int, SimulationHashTableViewModel>();

        var sides = new Dictionary<int, OperatorSides>();

        var aggregates = new Dictionary<int, SimulationAggregateViewModel>();

        var segments = new Dictionary<int, SimulationSegmentViewModel>();

        WirePanes(operators,
                  tabsByNode,
                  visuals,
                  colours,
                  nodeFor,
                  palette,
                  heldRows,
                  hashTables,
                  localHashTables,
                  sides,
                  aggregates,
                  segments);

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
                                  localHashTables,
                                  heldRows,
                                  sides,
                                  aggregates,
                                  segments);

        return new SimulationLayout
        {
            Tabs = [.. operators.Where(o => o is not (BatchToRowDefinition or RowToBatchDefinition))
                                .Select(o => tabsByNode[o.NodeId])],
            Nodes = nodes,
            Palette = palette
        };
    }

    private static UnaryDefinition? Adapter(IteratorDefinition definition)
        => definition is BatchToRowDefinition or RowToBatchDefinition ? (UnaryDefinition)definition : null;

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
            HashAggregateDefinition or BatchHashAggregateDefinition => "Hash Match",
            ComputeScalarDefinition or BatchComputeScalarDefinition => "Compute Scalar",
            FilterDefinition or BatchFilterDefinition => "Filter",
            SegmentDefinition => "Segment",
            SequenceProjectDefinition => "Sequence Project",
            SeekDefinition => "Index Seek",
            RangeDefinition => "Index Scan",
            HeapFetchDefinition => "RID Lookup",
            AllocationScanDefinition => "Table Scan",
            ColumnstoreScanDefinition => "Columnstore Index Scan",
            BatchToRowDefinition => "Batch Mode Adapter",
            RowToBatchDefinition => "Row Mode Adapter",
            _ => "Operator"
        };

    private static Dictionary<int, SimulationOperatorViewModel> CreateTabs(IReadOnlyList<IteratorDefinition> operators,
                                                                      SimulationBlobPalette palette,
                                                                      Func<int, PlanNode?> nodeFor)
    {
        var tabsByNode = new Dictionary<int, SimulationOperatorViewModel>();

        foreach (var op in operators)
        {
            var title = op.NodeId < 0 ? OperatorTitle(op) : $"{OperatorTitle(op)} ({op.NodeId})";

            var tab = new SimulationOperatorViewModel(op.NodeId, title, OperatorDescription(op));

            ApplyHeader(tab, op, nodeFor(op.NodeId));

            tab.IsBatchMode = op is IBatchDefinition || nodeFor(op.NodeId)?.IsBatchMode == true;

            tab.BlobPalette = palette;

            tabsByNode[op.NodeId] = tab;
        }

        return tabsByNode;
    }

    private static void WirePanes(IReadOnlyList<IteratorDefinition> operators,
                                  IReadOnlyDictionary<int, SimulationOperatorViewModel> tabsByNode,
                                  IReadOnlyDictionary<int, SimulationVisualViewModel> visuals,
                                  IReadOnlyDictionary<int, Color> colours,
                                  Func<int, PlanNode?> nodeFor,
                                  SimulationBlobPalette palette,
                                  Dictionary<(int NodeId, int InputIndex), SimulationHeldRowsViewModel> heldRows,
                                  Dictionary<int, SimulationHashTableViewModel> hashTables,
                                  Dictionary<int, SimulationHashTableViewModel> localHashTables,
                                  Dictionary<int, OperatorSides> sides,
                                  Dictionary<int, SimulationAggregateViewModel> aggregates,
                                  Dictionary<int, SimulationSegmentViewModel> segments)
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
                        var hashTable = new SimulationHashTableViewModel(HashTableFilter(hash.Build.Source, nodeFor));

                        hashTables[op.NodeId] = hashTable;

                        tab.OuterBottom = new SimulationPane(SimulationPaneKind.HashTable, hashTable);
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
                SelectDefinition => new SimulationPane(SimulationPaneKind.RowStream, tab.Output, "Results"),
                StreamAggregateDefinition aggregate => AggregatePane(aggregates, aggregate),
                SegmentDefinition segment => SegmentPane(segments, segment),
                BatchHashAggregateDefinition => BatchHashAggregatePane(hashTables, localHashTables, op.NodeId),
                HashAggregateDefinition => HashAggregatePane(hashTables, op.NodeId),
                _ when visuals.TryGetValue(op.NodeId, out var visual) => new SimulationPane(SimulationPaneKind.Visual, visual),
                _ => SimulationPane.Empty
            };
        }
    }

    private static void ConfigureRootTab(IteratorDefinition definition, IReadOnlyDictionary<int, SimulationOperatorViewModel> tabsByNode)
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

    private static Dictionary<int, SimulationVisualViewModel> MapSourceVisuals(IteratorDefinition definition,
                                                                          IReadOnlyDictionary<int, SimulationVisualViewModel> visuals)
    {
        var sourceVisuals = new Dictionary<int, SimulationVisualViewModel>();

        foreach (var source in SimulationSourceCollector.Collect(definition))
        {
            if (!sourceVisuals.ContainsKey(source.OperatorNodeId) && visuals.TryGetValue(source.NodeId, out var visual))
            {
                sourceVisuals[source.OperatorNodeId] = visual;
            }
        }

        return sourceVisuals;
    }

    private static Dictionary<int, SimulationNodeContext> BuildContexts(IReadOnlyList<int> ordered,
                                                                   IReadOnlyDictionary<int, IteratorDefinition> definitions,
                                                                   IReadOnlyDictionary<int, int> depths,
                                                                   IReadOnlyDictionary<int, Color> colours,
                                                                   IReadOnlyDictionary<int, SimulationOperatorViewModel> tabsByNode,
                                                                   IReadOnlyDictionary<int, SimulationVisualViewModel> visuals,
                                                                   IReadOnlyDictionary<int, SimulationVisualViewModel> sourceVisuals,
                                                                   IReadOnlyDictionary<int, SimulationHashTableViewModel> hashTables,
                                                                   IReadOnlyDictionary<int, SimulationHashTableViewModel> localHashTables,
                                                                   IReadOnlyDictionary<(int NodeId, int InputIndex), SimulationHeldRowsViewModel> heldRows,
                                                                   IReadOnlyDictionary<int, OperatorSides> sides,
                                                                   IReadOnlyDictionary<int, SimulationAggregateViewModel> aggregates,
                                                                   IReadOnlyDictionary<int, SimulationSegmentViewModel> segments)
    {
        var nodes = new Dictionary<int, SimulationNodeContext>();

        foreach (var nodeId in ordered)
        {
            var (outer, inner) = DefinitionTreeWalker.Inputs(definitions[nodeId]);

            var held = heldRows.Where(h => h.Key.NodeId == nodeId)
                               .ToDictionary(h => h.Key.InputIndex, h => h.Value);

            nodes[nodeId] = new SimulationNodeContext
            {
                Definition = definitions[nodeId],
                Depth = depths[nodeId],
                Colour = colours[nodeId],
                InputNodes = (outer?.NodeId ?? -1, inner?.NodeId ?? -1),
                Tab = tabsByNode.GetValueOrDefault(nodeId),
                LocalHashTable = localHashTables.GetValueOrDefault(nodeId),
                Visual = visuals.GetValueOrDefault(nodeId),
                SourceVisual = sourceVisuals.GetValueOrDefault(nodeId),
                HashTable = hashTables.GetValueOrDefault(nodeId),
                Aggregates = aggregates.GetValueOrDefault(nodeId),
                Segment = segments.GetValueOrDefault(nodeId),
                HeldRows = held,
                Sides = sides.GetValueOrDefault(nodeId)
            };
        }

        return nodes;
    }

    private static IEnumerable<SimulationInputRow> InputRowsOf(IteratorDefinition definition,
                                                          IReadOnlyDictionary<int, Color> colours,
                                                          Func<int, PlanNode?> nodeFor,
                                                          SimulationBlobPalette palette)
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

    private static SimulationInputRow InputRow(IteratorDefinition input,
                                          string label,
                                          IReadOnlyDictionary<int, Color> colours,
                                          Func<int, PlanNode?> nodeFor,
                                          SimulationBlobPalette palette,
                                          bool hasRowCount = true)
    {
        var adapter = Adapter(input);

        if (adapter is not null)
        {
            input = adapter.Source;
        }

        var header = OperatorHeader.For(input, nodeFor(input.NodeId));

        if (adapter is not null)
        {
            header = header with { Heading = $"{DisplayName(adapter)} : {header.Heading}" };
        }

        return new SimulationInputRow(input.NodeId, header.Heading)
        {
            Label = label,
            Blob = Accent(colours, input.NodeId) is { } accent ? palette.For(input.NodeId, accent) : null,
            Icon = header.Icon is { } icon ? new SvgImageSource(icon) : null,
            HasRowCount = hasRowCount
        };
    }

    private static Dictionary<int, Color> BuildColours(IReadOnlyList<int> ordered,
                                                       IReadOnlyDictionary<int, SimulationVisualViewModel> visuals)
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

        if (definition is BatchHashAggregateDefinition batchAggregate)
        {
            var hashed = string.Join(", ", batchAggregate.Aggregates.Select(a => a.ToText()));

            return $"{hashed} GROUP BY {string.Join(", ", batchAggregate.GroupBy)}".TrimStart();
        }

        if (definition is ComputeScalarDefinition compute)
        {
            return string.Join(", ", compute.Columns.Select(c => c.Name));
        }

        if (definition is FilterDefinition or BatchFilterDefinition)
        {
            return definition.Residual is null ? string.Empty : PredicateText.From(definition.Residual).ToString();
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
            HashAggregateDefinition or BatchHashAggregateDefinition => ("Input", string.Empty),
            ComputeScalarDefinition => ("Input", string.Empty),
            FilterDefinition or BatchFilterDefinition => ("Input", string.Empty),
            SegmentDefinition => ("Input", string.Empty),
            SequenceProjectDefinition => ("Input", string.Empty),
            _ => ("Outer Input", "Inner Input")
        };

    private static SimulationPane InputPane(IteratorDefinition? input,
                                       string label,
                                       IReadOnlyDictionary<int, SimulationOperatorViewModel> tabsByNode,
                                       IReadOnlyDictionary<int, SimulationVisualViewModel> visuals,
                                       IReadOnlyDictionary<int, Color> colours,
                                       Func<int, PlanNode?> nodeFor)
    {
        if (input is null)
        {
            return SimulationPane.Empty;
        }

        var adapter = Adapter(input);

        if (adapter is not null)
        {
            input = adapter.Source;
        }

        var header = OperatorHeader.For(input, nodeFor(input.NodeId));

        if (adapter is not null)
        {
            header = header with { Heading = $"{DisplayName(adapter)} : {header.Heading}" };
        }

        if (tabsByNode.TryGetValue(input.NodeId, out var tab))
        {
            return new SimulationPane(SimulationPaneKind.RowStream, tab.Output, label)
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
            return new SimulationPane(SimulationPaneKind.Visual, visual, label)
            {
                SourceNodeId = input.NodeId,
                AccentColour = Accent(colours, input.NodeId),
                Icon = header.Icon,
                Heading = header.Heading,
                Subheading = visual.AllocationUnit.DisplayName()
            };
        }

        return SimulationPane.Empty;
    }

    private static void ApplyHeader(SimulationOperatorViewModel tab, IteratorDefinition definition, PlanNode? node)
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

    private static SimulationPane HashAggregatePane(Dictionary<int, SimulationHashTableViewModel> hashTables, int nodeId)
    {
        var hashTable = new SimulationHashTableViewModel(RecordColumnFilter.All);

        hashTables[nodeId] = hashTable;

        return new SimulationPane(SimulationPaneKind.HashTable, hashTable, "Hash Table");
    }

    private static SimulationPane BatchHashAggregatePane(Dictionary<int, SimulationHashTableViewModel> hashTables,
                                                    Dictionary<int, SimulationHashTableViewModel> localHashTables,
                                                    int nodeId)
    {
        var local = new SimulationHashTableViewModel(RecordColumnFilter.All);

        var global = new SimulationHashTableViewModel(RecordColumnFilter.All);

        localHashTables[nodeId] = local;

        hashTables[nodeId] = global;

        return new SimulationPane(SimulationPaneKind.HashTables, new SimulationHashTablePair(local, global), "Hash Tables");
    }

    private static SimulationPane AggregatePane(Dictionary<int, SimulationAggregateViewModel> aggregates, StreamAggregateDefinition definition)
    {
        var viewModel = new SimulationAggregateViewModel(definition.Aggregates, definition.GroupBy);

        aggregates[definition.NodeId] = viewModel;

        return new SimulationPane(SimulationPaneKind.Aggregates, viewModel, "Aggregates");
    }

    private static SimulationPane SegmentPane(Dictionary<int, SimulationSegmentViewModel> segments, SegmentDefinition definition)
    {
        var viewModel = new SimulationSegmentViewModel(definition.GroupBy);

        segments[definition.NodeId] = viewModel;

        return new SimulationPane(SimulationPaneKind.Segment, viewModel, "Segment");
    }

    private static SimulationPane HeldPane(Dictionary<(int NodeId, int InputIndex), SimulationHeldRowsViewModel> heldRows,
                                      int nodeId,
                                      int inputIndex)
    {
        var held = new SimulationHeldRowsViewModel();

        heldRows[(nodeId, inputIndex)] = held;

        return new SimulationPane(SimulationPaneKind.HeldRows, held);
    }

    private static RecordColumnFilter HashTableFilter(IteratorDefinition build, Func<int, PlanNode?> nodeFor)
        => RecordColumnFilter.For(nodeFor(Unwrap(build).NodeId));

    private static int OperatorNodeIdOf(IteratorDefinition? side)
        => side is not null && Unwrap(side) is JoinDefinition join ? join.NodeId : -1;

    /// <summary>
    /// The objects read anywhere below one side of a join, which is what says whose column an operator is asked for
    /// </summary>
    private static HashSet<string> TablesUnder(IteratorDefinition? side, IReadOnlyDictionary<int, SimulationVisualViewModel> visuals)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (side is null)
        {
            return tables;
        }

        foreach (var source in SimulationSourceCollector.Collect(side))
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
