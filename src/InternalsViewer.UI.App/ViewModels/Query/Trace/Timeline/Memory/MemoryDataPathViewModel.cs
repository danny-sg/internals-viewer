using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.DataPath;
using InternalsViewer.Query.FullTrace.DataPath.Enums;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Helpers.Converters.Calls;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Microsoft.UI;
using Windows.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed class MemoryDataPathViewModel : MemoryStructureViewModel
{
    private const string DataPathTitle = "Data Path";

    private const string StatementName = "Statement";

    private const string ReadsPrefix = "Reads ";

    private const string NoOperators = "No Operators";

    private const string NoColumns = "No Output Columns";

    private const string RecordOffsetPrefix = "Record Offset ";

    private const string VariableColumnPrefix = "Variable Column ";

    private const string OutputName = "Output";

    private const string BufferPoolBadge = "Buffer Pool";

    private const string BufferFrameBadge = "Buffer Pool Frame";

    private const string WorkfilePageBadge = "Workfile Page";

    private const string PageTypeName = "Page";

    private const string EntryColumnName = "Entry";

    private const string TypeFieldName = "Type";

    private const int EntryColumnWidth = 160;

    private const int EntryFieldWidth = 120;

    private const int DataColumnWidth = 220;

    private const string DataColumnName = "Data";

    private const string DataAreaName = "Data Area";

    private const string VariableText = "(Variable)";

    private const string KeysAndPredicatesName = "Keys And Predicates";

    private const string NotSeenName = "Not Seen In Memory";

    private const string PointerName = "Pointer";

    private const int MaximumDepth = 32;

    private static readonly string RecordOffsetName = MemoryBindingsViewModel.GetFieldName(ColumnBindingField.RecordOffset);

    private static readonly HashSet<string> OffsetFieldNames =
    [
        RecordOffsetName,
        MemoryBindingsViewModel.GetFieldName(ColumnBindingField.StatusOffset),
        MemoryBindingsViewModel.GetFieldName(ColumnBindingField.LengthOffset),
        MemoryBindingsViewModel.GetFieldName(ColumnBindingField.ValueOffset)
    ];

    private static readonly IReadOnlyList<MemoryField> BindingFields =
        [.. AccessorBindingLayout.ColumnFields
                                 .Where(f => !f.IsInfrequent)
                                 .Select(f => new MemoryField(MemoryBindingsViewModel.GetFieldName(f.Field), f.Offset, f.Length))];

    private static readonly Color ReferenceColour = Color.FromArgb(255, 0xDD, 0xDD, 0xDD);

    public MemoryDataPathViewModel(MemoryHistoryViewModel history, TracePosition position, ITraceNavigator navigator)
        : base(history, position, navigator)
    {
        Loader = new PlayheadLoader<HexImage>(LoadInterval, Apply);

        IsApplying = true;

        BuildTree(null);

        IsApplying = false;

        Show();
    }

    public override string Title => DataPathTitle;

    public override string Key => DataPathTitle;

    protected override HexImage? Image => Loaded;

    protected override bool HasEntryDetails => false;

    private PlayheadLoader<HexImage> Loader { get; }

    private HexImage? Loaded { get; set; }

    private Dictionary<string, List<MemoryField>> LinkFields { get; } = [];

    private string? ScrolledKey { get; set; }

    private int NodeCount { get; set; }

    private Dictionary<int, DataPathOperator> Operators { get; set; } = [];

    private Dictionary<PlanOutputRouteNode, PlanOutputRouteNode> Containers { get; } = [];

    private Dictionary<MemoryStructureNode, PlanOutputRouteNode> RouteNodes { get; } = [];

    private DataPathGraph? OutputGraph { get; set; }

    private DataPathMap Map { get; set; } = DataPathMap.Empty;

    private Dictionary<string, PredicateText> Expressions { get; } = [];

    private string? EntryNodeKey { get; set; }

    private string? OutlineKey { get; set; }

    private List<(DataPathField Field, MemoryStructureNode Node)> ColumnNodes { get; } = [];

    private List<(DataPathField Field, MemoryStructureNode Node)> ValueNodes { get; } = [];

    public bool IsPlanObjectVisible
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            IsApplying = true;

            LinkFields.Clear();

            BuildTree(SelectedNode?.Address);

            IsApplying = false;

            Show();
        }
    }

    public override void SelectOffset(int offset)
    {
        var previous = MaskedMarker;

        SelectFieldAt(offset);

        MaskAt(offset, previous);
    }

    public override void Close()
    {
        Loader.Cancel();

        SetOutputGraph(null);

        Loaded = null;

        base.Close();
    }

    protected override void Load()
    {
        var history = History;

        var address = SelectedNode is { } selected ? GetHexAddress(selected) : HexAddress;

        var position = Position;

        Loader.Request(() => HexImage.Read(history, address, position));
    }

    protected override void ShowDetail(MemoryStructureNode node)
    {
        RefreshValues();

        Graph = node.Type == MemoryStructureNodeType.Statement ? OutputGraph : null;

        Expression = node.Type == MemoryStructureNodeType.Expression ? Expressions.GetValueOrDefault(node.Key) : null;

        var entryOffset = (int?)null;

        if (GetEntries(node) is var (array, entry))
        {
            entryOffset = ShowEntries(node, array, entry);
        }
        else
        {
            OutlineKey = null;

            var fields = GetFields(node);

            ShowFields(node, fields, CreateColours(fields.Count));
        }

        if (node.Key != ScrolledKey && Image is { } image)
        {
            ScrolledKey = node.Key;

            ScrollToOffset = null;

            ScrollToOffset = entryOffset ?? image.Offset;
        }
    }

    protected override ResultDetails GetEntryDetails(ResultRow<long> row) => new(string.Empty, []);

    protected override MemoryStructureNode? GetPointerNode(ulong value) => Nodes.GetValueOrDefault(value);

    protected override void OpenPointer(MemoryStructureNode node, ulong value) => Select(node);

    protected override ulong GetHexAddress(MemoryStructureNode node)
    {
        if (RouteNodes.TryGetValue(node, out var route) && GetCurrentRecord(route.Link) is { } record)
        {
            return record;
        }

        return GetEntries(node) is var (array, _) ? array.Link.Address : base.GetHexAddress(node);
    }

    protected override string GetPointerText(MemoryStructureNode node, ulong pointer)
        => History.GetPointerTarget(pointer, Position) ?? node.Name;

    private void RefreshValues()
    {
        if (OutputGraph is null && Map.Roots.Count > 0)
        {
            SetOutputGraph(DataPathGraphBuilder.Build(History, Map, Map.Roots, OpenAddress));
        }

        if (OutputGraph is { } graph)
        {
            DataPathGraphBuilder.Refresh(graph, History, new MemoryHistoryReader(History.Store, Position));
        }
    }

    private void SetOutputGraph(DataPathGraph? graph)
    {
        if (OutputGraph is { } previous)
        {
            previous.Changed -= OnValuesChanged;
        }

        OutputGraph = graph;

        if (graph is not null)
        {
            graph.Changed += OnValuesChanged;
        }
    }

    private void OnValuesChanged(object? sender, EventArgs e)
    {
        var values = new Dictionary<DataPathField, string>();

        foreach (var stage in OutputGraph?.Stages ?? [])
        {
            values.TryAdd(stage.Field, stage.Value);
        }

        foreach (var column in OutputGraph?.Columns ?? [])
        {
            values[column.Field] = column.Value;
        }

        foreach (var (field, node) in ValueNodes.Concat(ColumnNodes))
        {
            node.Value = values.GetValueOrDefault(field) ?? string.Empty;
        }
    }

    private void OpenAddress(ulong address)
    {
        var position = Position;

        if (Nodes.GetValueOrDefault(address) is { } node)
        {
            Select(node);
        }
        else if (address != 0 && History.GetBlock(address, position) is >= 0 and var block)
        {
            Navigator.OpenMemory(block, address, position);
        }
    }

    private void Apply(HexImage image)
    {
        Loaded = image;

        RememberFirst();

        Show();
    }

    private List<MemoryField> GetFields(MemoryStructureNode node)
    {
        var fields = LinkFields.GetValueOrDefault(node.Key) ?? [];

        if (Image is not { Offset: 0, Block: >= 0, Data: { } data } image)
        {
            return fields;
        }

        var block = History.Store.Blocks[image.Block];

        var className = History.AddressMap.GetClassName(block.Address, block.FromPosition, block.ToPosition);

        if (History.IteratorTargets.TryGetValue(node.Address, out var target))
        {
            return IteratorLayout.GetFields(className, target.Type, data, History.IteratorTargets);
        }

        return [.. fields.Concat(PlanOutputValueLayout.GetFields(className)).DistinctBy(f => f.Offset).OrderBy(f => f.Offset)];
    }

    private void BuildTree(ulong? address)
    {
        var history = History;

        Operators = history.IteratorTargets
                           .Where(t => t.Value.Operator?.PlanNodeIdentifier is not null)
                           .GroupBy(t => t.Value.Operator!.PlanNodeIdentifier!.NodeId)
                           .ToDictionary(g => g.Key,
                                         g => new DataPathOperator(g.First().Value.Operator!,
                                                                   GetTarget(g, IteratorTargetType.Iterator),
                                                                   GetTarget(g, IteratorTargetType.PlanObject)));

        if (Operators.Count == 0)
        {
            EmptyText = NoOperators;

            return;
        }

        Map = history.GetDataPathMap(IsPlanObjectVisible);

        Containers.Clear();

        RouteNodes.Clear();

        ValueNodes.Clear();

        ColumnNodes.Clear();

        Expressions.Clear();

        SetOutputGraph(null);

        foreach (var root in Map.Routes.Values.SelectMany(r => r.FromIterator.Concat(r.FromPlanObject)))
        {
            AddContainers(root);
        }

        var statement = Node(MemoryStructureNodeType.Statement, Map.Statement?.PhysicalOperator ?? StatementName, 0, null);

        foreach (var column in Map.Columns)
        {
            statement.Children.Add(CreateColumn(column, statement));
        }

        if (statement.Children.Count == 0)
        {
            EmptyText = NoColumns;

            return;
        }

        List<MemoryStructureNode> roots = [statement];

        if (Map.Roots.Where(r => !r.Node.IsStatement).ToList() is { Count: > 0 } used)
        {
            var group = Node(MemoryStructureNodeType.Statement, KeysAndPredicatesName, 0, null);

            foreach (var field in used)
            {
                group.Children.Add(CreateColumn(field, group));
            }

            roots.Add(group);
        }

        var nodes = new Dictionary<ulong, MemoryStructureNode>();

        foreach (var node in roots.SelectMany(Flatten).Where(n => n.HasData))
        {
            nodes.TryAdd(node.Address, node);
        }

        Nodes = nodes;

        Roots = roots;

        SelectedNode = roots.SelectMany(Flatten).FirstOrDefault(n => n.IsSelectable && n.Address == address)
                       ?? roots.SelectMany(Flatten).First(n => n.IsSelectable);

        SelectedKey = SelectedNode.Key;
    }

    private void AddContainers(PlanOutputRouteNode node)
    {
        foreach (var child in node.Children)
        {
            Containers[child] = node;

            AddContainers(child);
        }
    }

    private MemoryStructureNode CreateColumn(DataPathField field, MemoryStructureNode parent)
    {
        var held = field.Node.IsStatement
            ? field is { Definition: null, Inputs: [var only] } ? only.Source.Held : null
            : field.Held;

        var node = new MemoryStructureNode(MemoryStructureNodeType.Column, field.Name, held?.Link.Address ?? 0, -1, parent)
        {
            Index = NodeCount++,
            ColumnType = field.Type,
            IsAddressVisible = false
        };

        if (DataPathText.GetUses(field.Uses) is { Length: > 0 } uses)
        {
            node.Badges.Add(CreateBadge(uses, DataPathText.UseColour));
        }

        if (held is not null)
        {
            LinkFields[node.Key] = GetLinkFields(held);
        }

        ColumnNodes.Add((field, node));

        if (!field.Node.IsStatement)
        {
            AddField(field, null, node, 0);

            return node;
        }

        var inputParent = node;

        if (field.Expression is { } expression)
        {
            inputParent = CreateExpression(expression, field.Definition, node);

            node.Children.Add(inputParent);
        }

        foreach (var input in field.Inputs)
        {
            AddField(input.Source, input, inputParent, 0);
        }

        return node;
    }

    private MemoryStructureNode CreateExpression(PlanNode expression, DefinedValueInfo? value, MemoryStructureNode parent)
    {
        var node = new MemoryStructureNode(MemoryStructureNodeType.Expression, GetDefinition(expression), 0, -1, parent)
        {
            Index = NodeCount++,
            IsAddressVisible = false
        };

        if (value is not null)
        {
            Expressions[node.Key] = DataPathText.GetExpression(value);
        }

        return node;
    }

    private void AddExpression(DataPathField field, MemoryStructureNode operatorNode)
    {
        if (field.Expression is not { } expression)
        {
            return;
        }

        var node = CreateExpression(expression, field.Definition, operatorNode);

        operatorNode.Children.Add(node);

        if (field.Kind == DataPathFieldKind.Computed)
        {
            ValueNodes.Add((field, node));
        }
    }

    private PlanOutputRouteNode GetDisplayRoot(PlanOutputRouteNode held)
        => held.Link.Type == PlanOutputLinkType.Record
           && GetContainers(held).LastOrDefault(c => c.Link.Type == PlanOutputLinkType.Rowset) is { } rowset
            ? rowset
            : held;

    private string GetHolderType(PlanOutputLink link)
        => link.Type is PlanOutputLinkType.Rowset or PlanOutputLinkType.Row
           && History.GetBlock(link.Address, Position) is >= 0 and var block
            ? History.GetTypeName(block)
            : string.Empty;

    private (PlanOutputRouteNode Array, PlanOutputRouteNode? Entry)? GetEntries(MemoryStructureNode node)
    {
        if (!RouteNodes.TryGetValue(node, out var route))
        {
            return null;
        }

        if (route.Link.Type is PlanOutputLinkType.RowBuffer or PlanOutputLinkType.ColumnBindings)
        {
            return (route, null);
        }

        return route.Link.Type is PlanOutputLinkType.BufferEntry or PlanOutputLinkType.ColumnBinding
               && Containers.GetValueOrDefault(route) is { } array
               && array.Link.Type is PlanOutputLinkType.RowBuffer or PlanOutputLinkType.ColumnBindings
            ? (array, route)
            : null;
    }

    private int? ShowEntries(MemoryStructureNode node, PlanOutputRouteNode array, PlanOutputRouteNode? entry)
    {
        FieldTable = null;

        IsPageVisible = false;

        if (node.Key != EntryNodeKey)
        {
            EntryNodeKey = node.Key;

            SelectedItem = entry?.Link.Address;
        }

        if (Image is not { Data: { } data } image)
        {
            EntryTable = null;

            Outlines = null;

            return null;
        }

        var isBinding = array.Link.Type == PlanOutputLinkType.ColumnBindings;

        var fields = isBinding ? BindingFields : EngineLayout.RowBuffer.EntryFields;

        var size = isBinding ? AccessorBindingLayout.ColumnSize : EngineLayout.RowBuffer.EntryField.Length;

        var entryType = isBinding ? PlanOutputLinkType.ColumnBinding : PlanOutputLinkType.BufferEntry;

        var entries = array.Children.Where(c => c.Link.Type == entryType).OrderBy(c => c.Link.Address).ToList();

        var colours = CreateColours(fields.Count);

        var start = HexAddress - (ulong)image.Offset;

        var offsets = entries.Select(e => (int)(e.Link.Address - start)).ToList();

        var values = offsets.Select(o => fields.Select(f => ReadValue(data, o + f.Offset, f.Length)).ToList()).ToList();

        var spans = offsets.Select(o => isBinding ? null : GetDataSpan(data, o, start)).ToList();

        var names = GetNodeId(node) is { } nodeId ? GetColumnNames(nodeId) : [];

        var labels = entries.Select(e => GetEntryName(e, names)).ToList();

        var key = $"{node.Key} {string.Join(",", values.SelectMany(v => v))}";

        if (key != TableKey || EntryTable is null)
        {
            TableKey = key;

            List<ResultColumn> columns =
            [
                new(0, EntryColumnName, typeof(string), false) { Width = EntryColumnWidth },
                .. fields.Select((f, i) => new ResultColumn(i + 1, f.Name, typeof(string), true)
                {
                    Width = EntryFieldWidth,
                    MarkerColour = colours[i]
                })
            ];

            if (!isBinding)
            {
                columns.Add(new ResultColumn(columns.Count, DataColumnName, typeof(string), true) { Width = DataColumnWidth });
            }

            var rows = entries.Select((e, i) => new ResultRow<long>([labels[i],
                                                                     .. fields.Select((f, j) => GetEntryCell(f, values[i][j])),
                                                                     .. isBinding ? Array.Empty<object?>() : [GetEntryData(values[i])]])
            {
                Id = (long)e.Link.Address
            }).ToList();

            EntryTable = new QueryResultSet { Columns = columns, Rows = rows };
        }

        SelectItemRow();

        var selected = SelectedItem is { } item ? entries.FindIndex(e => e.Link.Address == item) : -1;

        var valueIndex = fields.Select((f, i) => (Field: f, Index: i))
                               .FirstOrDefault(f => f.Field == EngineLayout.RowBuffer.ValueField)
                               .Index;

        var dataColour = Lighten(colours[valueIndex]);

        IEnumerable<Marker> CreateMarkers()
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (selected >= 0 && index != selected)
                {
                    continue;
                }

                for (var field = 0; field < fields.Count; field++)
                {
                    var layout = fields[field];

                    yield return CreateMarker(layout.Name, offsets[index] + layout.Offset, layout.Length, colours[field]);
                }

                if (spans[index] is { } span)
                {
                    yield return CreateMarker($"{labels[index]} {DataColumnName}", span.Offset, span.Length, dataColour);
                }
            }
        }

        SetMarkers($"{node.Key} entries {selected} {string.Join(",", spans)}", CreateMarkers);

        ShowOutlines(node, selected >= 0 ? (offsets[selected], size, labels[selected], spans[selected]) : null, offsets, size, spans);

        return selected >= 0 ? offsets[selected] : null;
    }

    private void ShowOutlines(MemoryStructureNode node,
                              (int Offset, int Size, string Label, (int Offset, int Length)? Data)? selected,
                              List<int> offsets,
                              int size,
                              List<(int Offset, int Length)?> spans)
    {
        var data = spans.OfType<(int Offset, int Length)>().ToList();

        var areaStart = offsets.Count > 0 ? offsets.Max() + size : 0;

        var areaEnd = data.Count > 0 ? data.Max(s => s.Offset + s.Length) : 0;

        var key = $"{node.Key} {selected} {areaStart} {areaEnd}";

        if (key == OutlineKey)
        {
            return;
        }

        OutlineKey = key;

        ItemOutline = selected is { } found
            ? new LogRecordAnnotation { Offset = found.Offset, Length = found.Size, Description = found.Label }
            : null;

        var outlines = new ObservableCollection<LogRecordAnnotation>();

        if (areaEnd > areaStart)
        {
            outlines.Add(new LogRecordAnnotation
            {
                Offset = areaStart,
                Length = areaEnd - areaStart,
                Description = DataAreaName,
                IsDashed = true
            });
        }

        if (selected is { Data: { } span } entry)
        {
            outlines.Add(new LogRecordAnnotation
            {
                Offset = span.Offset,
                Length = span.Length,
                Description = $"{entry.Label} {DataColumnName}",
                OutlineColour = Colors.OrangeRed
            });
        }

        Outlines = outlines;
    }

    private static (int Offset, int Length)? GetDataSpan(byte[] data, int entry, ulong start)
    {
        if (ReadValue(data, entry + EngineLayout.RowBuffer.StatusField.Offset, sizeof(byte)) is not 0
            || ReadValue(data, entry + EngineLayout.RowBuffer.LengthField.Offset, sizeof(ulong)) is not (> 0 and var length)
            || ReadValue(data, entry + EngineLayout.RowBuffer.ValueField.Offset, sizeof(ulong)) is not { } pointer
            || pointer < start
            || pointer >= start + (ulong)data.Length)
        {
            return null;
        }

        var offset = (int)(pointer - start);

        return (offset, (int)Math.Min(length, (ulong)(data.Length - offset)));
    }

    private string GetEntryData(IReadOnlyList<ulong?> entry)
    {
        if (entry is not [{ } status, { } type, { } value, { } length]
            || type == 0
            || !SqlTypeHelpers.TryGetSqlType((byte)type, out var sqlType))
        {
            return string.Empty;
        }

        var variant = new VariantLocator(VariantLocator.Create(0, (VariantStatus)status, value, (uint)length));

        return PlanOutputValueReader.ReadVariant(new MemoryHistoryReader(History.Store, Position), variant, sqlType) ?? string.Empty;
    }

    private static Colour Lighten(Colour colour)
        => Colour.FromArgb(255, (colour.R + byte.MaxValue) / 2, (colour.G + byte.MaxValue) / 2, (colour.B + byte.MaxValue) / 2);

    private object? GetEntryCell(MemoryField field, ulong? value)
    {
        if (field.Name == TypeFieldName && value is { } type && SqlTypeHelpers.TryGetSqlType((byte)type, out var sqlType))
        {
            return SqlDataTypeFormat.GetName(sqlType);
        }

        if (value is not { } found || !OffsetFieldNames.Contains(field.Name))
        {
            return GetValueCell(field, value);
        }

        var offset = field.Length == sizeof(int) ? (int)found : (long)found;

        return offset < 0 && field.Name == RecordOffsetName ? VariableText : $"0x{offset:X}";
    }

    private string GetEntryName(PlanOutputRouteNode entry, IReadOnlyList<string> names)
        => GetDetail(entry, names) is { Length: > 0 } name ? $"[{entry.Index}] {name}" : $"[{entry.Index}]";

    private int? GetNodeId(MemoryStructureNode node)
    {
        if (FindOperatorNode(node) is not { } operatorNode)
        {
            return null;
        }

        foreach (var (nodeId, found) in Operators)
        {
            if (found.Iterator == operatorNode.Address || found.PlanObject == operatorNode.Address)
            {
                return nodeId;
            }
        }

        return null;
    }

    private static string GetDefinition(PlanNode expression) => string.Join(", ", DataPathText.GetDefinitions(expression));

    private MemoryStructureNode? FindRouteNode(MemoryStructureNode root, PlanOutputRouteNode route)
        => root.Children.SelectMany(Flatten).FirstOrDefault(n => RouteNodes.TryGetValue(n, out var found) && found == route);

    private ulong? GetCurrentRecord(PlanOutputLink link)
        => link.Type == PlanOutputLinkType.Record
           && new MemoryHistoryReader(History.Store, Position).TryRead(link.Source + (ulong)link.SourceOffset, out ulong record)
           && record != 0
            ? record
            : null;

    private MemoryStructureBadge? GetRecordBadge(PlanOutputLink link)
    {
        var address = GetCurrentRecord(link) ?? link.Address;

        if (History.GetBufferPage(address, Position) is { } page)
        {
            return CreateBadge(page.IsBuffer ? BufferPoolBadge : BufferFrameBadge);
        }

        return History.GetBlock(address, Position) is >= 0 and var block && History.GetTypeName(block) == PageTypeName
            ? CreateBadge(WorkfilePageBadge)
            : null;
    }

    private static MemoryStructureNode? FindOperatorNode(MemoryStructureNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Type == MemoryStructureNodeType.Operator)
            {
                return current;
            }
        }

        return null;
    }

    private void AddField(DataPathField field, DataPathLink? link, MemoryStructureNode parent, int depth)
    {
        var nodeId = field.Node.NodeId;

        if (!Operators.ContainsKey(nodeId) || depth > MaximumDepth)
        {
            return;
        }

        var holder = link is { Readers: { Count: > 0 } readers } && FindOperatorNode(parent) is { } consumer
            ? GetReaderNode(readers, consumer)
            : parent;

        var operatorNode = CreateOperator(field, holder, parent.Type == MemoryStructureNodeType.Expression ? field.Name : null, link);

        if (field.Kind != DataPathFieldKind.Computed)
        {
            ValueNodes.Add((field, operatorNode));
        }

        holder.Children.Add(operatorNode);

        var names = GetColumnNames(nodeId);

        var route = Map.Routes.GetValueOrDefault(nodeId);

        if (field.Held is { } held)
        {
            operatorNode.Children.Add(CreateObject(GetDisplayRoot(held), operatorNode, names));

            if (route is not null && FindBinding(route, held) is { } binding)
            {
                operatorNode.Children.Add(CreateObject(binding, operatorNode, names));
            }
        }

        if (route is not null
            && IsPlanObjectVisible
            && field.Output is { } output
            && Flatten(route.FromPlanObject).FirstOrDefault(IsReferenceTo(GetColumnIndex(nodeId, output.Reference))) is { } compiled)
        {
            operatorNode.Children.Add(CreateObject(compiled, operatorNode, names));
        }

        AddExpression(field, operatorNode);

        foreach (var input in field.Inputs)
        {
            AddField(input.Source, input, operatorNode, depth + 1);
        }
    }

    private MemoryStructureNode GetReaderNode(IReadOnlyList<PlanOutputRouteNode> readers, MemoryStructureNode consumer)
    {
        var names = GetNodeId(consumer) is { } nodeId ? GetColumnNames(nodeId) : [];

        MemoryStructureNode? holder = null;

        foreach (var reader in readers)
        {
            var readerNode = FindRouteNode(consumer, reader);

            if (readerNode is null)
            {
                readerNode = CreateObject(reader, consumer, names);

                consumer.Children.Add(readerNode);
            }

            holder ??= readerNode;
        }

        return holder ?? consumer;
    }

    private MemoryStructureNode CreateOperator(DataPathField field, MemoryStructureNode parent, string? prefix, DataPathLink? link)
    {
        var found = Operators[field.Node.NodeId];

        var node = new MemoryStructureNode(MemoryStructureNodeType.Operator,
                                           prefix is { Length: > 0 } ? $"{prefix} › {found.Event.Label}" : found.Event.Label,
                                           found.Iterator != 0 ? found.Iterator : found.PlanObject,
                                           -1,
                                           parent)
        {
            Index = NodeCount++,
            Detail = field is { Expression: null, Definition: { } definition } ? DataPathText.GetText(definition) : string.Empty,
            IsAddressVisible = false
        };

        if (field.Stage is { } stage && PlanOutputLinkText.GetReferenceName(stage) is { Length: > 0 } reference)
        {
            node.Badges.Add(CreateBadge(reference));
        }

        if (field.Kind == DataPathFieldKind.Pointer)
        {
            node.Badges.Add(CreateBadge(PointerName));
        }

        if (DataPathText.GetUses(field.Uses) is { Length: > 0 } uses)
        {
            node.Badges.Add(CreateBadge(uses, DataPathText.UseColour));
        }

        if (link is { IsSeen: false })
        {
            node.Badges.Add(CreateBadge(NotSeenName));
        }

        return node;
    }

    private MemoryStructureNode CreateObject(PlanOutputRouteNode route, MemoryStructureNode parent, IReadOnlyList<string> names)
        => CreateLink(route, parent, names, Containers.TryGetValue(route, out var container) ? GetDisplayName(container) : null);

    private IEnumerable<PlanOutputRouteNode> GetContainers(PlanOutputRouteNode node)
    {
        for (var current = node; current is not null; current = Containers.GetValueOrDefault(current))
        {
            yield return current;
        }
    }

    private static MemoryStructureBadge CreateBadge(string reference, Color? colour = null)
        => (colour ?? PlanOutputLinkText.GetReferenceColour(reference)) is { } found
            ? new MemoryStructureBadge(reference, found) { TextColour = Colors.White }
            : new MemoryStructureBadge(reference, ReferenceColour);

    private static string? GetSide(MemoryField field)
    {
        if (field == EngineLayout.HashIterator.BuildInputValuesField)
        {
            return InputLabel.Build;
        }

        return field == EngineLayout.HashIterator.ProbeInputValuesField ? InputLabel.Probe : null;
    }

    private int GetColumnIndex(int nodeId, ulong reference)
        => History.GetOutputColumns(nodeId).ToList().FindIndex(c => c.Reference == reference);

    private List<string> GetColumnNames(int nodeId)
        => Operators.TryGetValue(nodeId, out var found)
            ? History.GetOutputColumnNames(found.Iterator != 0 ? found.Iterator : found.PlanObject, History.GetOutputColumns(nodeId).Count)
            : [];

    private static PlanOutputRouteNode? FindBinding(PlanOutputRoute route, PlanOutputRouteNode entry)
        => entry.Link.Type == PlanOutputLinkType.BufferEntry
            ? Flatten(route.FromIterator).FirstOrDefault(n => n.Link.Type == PlanOutputLinkType.ColumnBinding && n.Link.Slot == entry.Index)
            : null;

    private static Func<PlanOutputRouteNode, bool> IsReferenceTo(int column)
        => n => n.Link.Type == PlanOutputLinkType.Reference && n.Column == column;

    private static IEnumerable<PlanOutputRouteNode> Flatten(IEnumerable<PlanOutputRouteNode> nodes)
        => nodes.SelectMany(n => Flatten(n.Children).Prepend(n));

    private MemoryStructureNode CreateLink(PlanOutputRouteNode route,
                                           MemoryStructureNode parent,
                                           IReadOnlyList<string> names,
                                           string? container = null)
    {
        var link = route.Link;

        var name = GetDisplayName(route);

        var node = new MemoryStructureNode(MemoryStructureNodeType.Link,
                                           container is null ? name : $"{container} › {name}",
                                           link.Address,
                                           -1,
                                           parent)
        {
            Index = NodeCount++,
            Detail = GetDetail(route, names) is { Length: > 0 } detail ? detail : GetHolderType(link),
            ColumnType = link.ColumnType,
            IsAddressVisible = false
        };

        if (GetRecordBadge(link) is { } badge)
        {
            node.Badges.Add(badge);
        }

        foreach (var child in route.Children)
        {
            node.Children.Add(CreateLink(child, node, names));
        }

        LinkFields[node.Key] = GetLinkFields(route);

        RouteNodes[node] = route;

        return node;
    }

    private string GetDisplayName(PlanOutputRouteNode route)
    {
        var name = GetName(route.Link);

        return route.Index >= 0 ? $"{name} [{route.Index}]" : name;
    }

    private List<MemoryField> GetLinkFields(PlanOutputRouteNode route)
    {
        var link = route.Link;

        var fields = new List<MemoryField>();

        foreach (var child in route.Children)
        {
            fields.AddRange(GetChildFields(child.Link, child.Index, link.Address));
        }

        foreach (var target in route.Targets)
        {
            fields.AddRange(GetChildFields(target.Link, -1, link.Address));
        }

        if (fields.Count == 0)
        {
            fields.AddRange(GetOwnFields(link));
        }

        return [.. fields.DistinctBy(f => f.Offset).OrderBy(f => f.Offset)];
    }

    private string GetDetail(PlanOutputRouteNode route, IReadOnlyList<string> names)
    {
        if (route.Column >= 0 && route.Column < names.Count)
        {
            return names[route.Column];
        }

        if (GetRecordOffsetText(route.Link) is { Length: > 0 } offset)
        {
            return offset;
        }

        return route.Targets.Count > 0 && Operators.TryGetValue(route.Targets[0].NodeId, out var target)
            ? $"{ReadsPrefix}{target.Event.Label}"
            : string.Empty;
    }

    private static string GetRecordOffsetText(PlanOutputLink link)
        => link is { Type: PlanOutputLinkType.Reader or PlanOutputLinkType.Value, Offset: { } offset }
            ? offset < 0 ? $"{VariableColumnPrefix}{-offset}" : $"{RecordOffsetPrefix}0x{offset:X}"
            : string.Empty;

    private static bool IsEntryOf(PlanOutputLink link)
        => link.Type == PlanOutputLinkType.BufferEntry && link.Address == link.Source + (ulong)link.SourceOffset;

    private static IEnumerable<MemoryField> GetEntryFields(string prefix, int offset)
        => EngineLayout.RowBuffer.EntryFields.Select(f => f with { Name = $"{prefix}{f.Name}", Offset = offset + f.Offset });

    private static IEnumerable<MemoryField> GetOwnFields(PlanOutputLink link)
    {
        if (link.Type == PlanOutputLinkType.BufferEntry)
        {
            return GetEntryFields(string.Empty, 0);
        }

        if (link.Type == PlanOutputLinkType.ColumnBinding)
        {
            return AccessorBindingLayout.ColumnFields
                                        .Where(f => !f.IsInfrequent)
                                        .Select(f => new MemoryField(MemoryBindingsViewModel.GetFieldName(f.Field), f.Offset, f.Length));
        }

        return link.Field is { } field && link.Address == link.Source + (ulong)link.SourceOffset ? [field with { Offset = 0 }] : [];
    }

    private IEnumerable<MemoryField> GetChildFields(PlanOutputLink link, int index, ulong address)
    {
        var block = History.GetBlock(address, link.Position);

        if (block < 0 || History.GetBlock(link.Source, link.Position) != block || link.Source + (ulong)link.SourceOffset < address)
        {
            return [];
        }

        var offset = (int)(link.Source + (ulong)link.SourceOffset - address);

        var name = link.Field?.Name ?? PlanOutputLinkText.GetName(link.Type);

        if (index >= 0)
        {
            name = $"{name} [{index}]";
        }

        if (IsEntryOf(link))
        {
            return GetEntryFields($"{name} ", offset);
        }

        return [link.Field is { } field ? field with { Name = name, Offset = offset } : new MemoryField(name, offset, sizeof(ulong))];
    }

    private string GetName(PlanOutputLink link)
    {
        var name = link switch
        {
            { Type: PlanOutputLinkType.OutputValues } => OutputName,
            { Type: PlanOutputLinkType.InputValues, Field: { } field } => GetSide(field) ?? field.Name,
            { Type: PlanOutputLinkType.Executor or PlanOutputLinkType.Executors, Field: { } field } => field.Name,
            _ => PlanOutputLinkText.GetObjectName(link.ClassName) ?? PlanOutputLinkText.GetName(link.Type)
        };

        return History.IteratorTargets.TryGetValue(link.Address, out var target) ? $"{name} › {target.Label}" : name;
    }

    private static ulong GetTarget(IEnumerable<KeyValuePair<ulong, IteratorTarget>> targets, IteratorTargetType type)
        => targets.FirstOrDefault(t => t.Value.Type == type).Key;

    private MemoryStructureNode Node(MemoryStructureNodeType type, string name, ulong address, MemoryStructureNode? parent)
        => new(type, name, address, -1, parent) { Index = NodeCount++, IsAddressVisible = false };

    private sealed record DataPathOperator(ExecutionOperatorEvent Event, ulong Iterator, ulong PlanObject);
}
