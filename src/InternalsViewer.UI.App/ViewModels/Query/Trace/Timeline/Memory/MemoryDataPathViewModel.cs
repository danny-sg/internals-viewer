using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Helpers.Converters.Calls;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Microsoft.UI;
using Windows.UI;

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

    private Dictionary<int, PlanOutputRoute> Routes { get; set; } = [];

    private Dictionary<int, DataPathOperator> Operators { get; set; } = [];

    private Dictionary<PlanOutputRouteNode, PlanOutputRouteNode> Containers { get; } = [];

    private Dictionary<MemoryStructureNode, PlanOutputRouteNode> RouteNodes { get; } = [];

    private List<DataPathColumn> OutputColumns { get; } = [];

    private DataPathGraph? OutputGraph { get; set; }

    private DataPathPlan? Plan { get; set; }

    private Dictionary<string, PredicateText> Expressions { get; } = [];

    private MemoryStructureNode? CurrentColumn { get; set; }

    private string? EntryNodeKey { get; set; }

    private List<(DataPathColumn Column, MemoryStructureNode Node)> ColumnNodes { get; } = [];

    private List<(PlanOutputStage Stage, MemoryStructureNode Node)> ValueNodes { get; } = [];

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
        if (OutputGraph is null && OutputColumns.Count > 0)
        {
            SetOutputGraph(DataPathGraphBuilder.Build(History, Plan, OutputColumns, OpenAddress));
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
        var values = new Dictionary<PlanOutputStage, string>();

        foreach (var stage in OutputGraph?.Stages ?? [])
        {
            if (stage.Stage is { } output)
            {
                values.TryAdd(output, stage.Value);
            }
        }

        foreach (var (stage, node) in ValueNodes)
        {
            node.Value = values.GetValueOrDefault(stage) ?? string.Empty;
        }

        foreach (var (column, node) in ColumnNodes)
        {
            if (column.Expression is not null)
            {
                node.Value = column.Text;
            }
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

        Plan = DataPathGraphBuilder.GetPlan(history);

        var tops = Plan?.GetTops(Operators.ContainsKey)
                   ?? [.. Operators.Where(o => o.Value.Event.ParentNodeId is not { } parent || !Operators.ContainsKey(parent))
                                   .Select(o => o.Key)
                                   .Order()];

        if (tops.Count == 0)
        {
            EmptyText = NoOperators;

            return;
        }

        var statement = Node(MemoryStructureNodeType.Statement, Plan?.Statement.PhysicalOperator ?? StatementName, 0, null);

        Routes = history.GetDataPath([.. Operators.Select(o => GetRouteOperator(o.Key, o.Value))], IsPlanObjectVisible);

        Containers.Clear();

        RouteNodes.Clear();

        OutputColumns.Clear();

        ValueNodes.Clear();

        ColumnNodes.Clear();

        Expressions.Clear();

        SetOutputGraph(null);

        foreach (var root in Routes.Values.SelectMany(r => r.FromIterator.Concat(r.FromPlanObject)))
        {
            AddContainers(root);
        }

        var columns = Plan?.GetColumns(Operators.ContainsKey, GetOutputColumn) is { Count: > 0 } resolved ? resolved : GetTopColumns(tops);

        foreach (var column in columns)
        {
            var node = CreateColumn(column, statement);

            statement.Children.Add(node);

            ColumnNodes.Add((column, node));

            OutputColumns.Add(column);
        }

        if (statement.Children.Count == 0)
        {
            EmptyText = NoColumns;

            return;
        }

        List<MemoryStructureNode> roots = [statement];

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

    private MemoryStructureNode CreateColumn(DataPathColumn column, MemoryStructureNode parent)
    {
        DataPathInput? single = column is { Expression: null, Inputs: [var only] } ? only : null;

        var path = single is { } input ? History.GetOutputPath(input.NodeId, input.Column) : PlanOutputPath.Empty;

        var held = single is { } owner ? GetHeld(owner) : null;

        var node = new MemoryStructureNode(MemoryStructureNodeType.Column, column.Name, held?.Link.Address ?? 0, -1, parent)
        {
            Index = NodeCount++,
            ColumnType = column.Type,
            IsAddressVisible = false
        };

        CurrentColumn = node;

        if (held is not null)
        {
            LinkFields[node.Key] = GetLinkFields(held);
        }

        if (path.Stages.Count > 0)
        {
            ValueNodes.Add((path.Stages[0], node));

            AddStage(path, 0, null, node);
        }

        if (column.Expression is not { } expression)
        {
            return node;
        }

        var expressionNode = CreateExpression(expression, column.Definition, node);

        node.Children.Add(expressionNode);

        foreach (var source in column.Inputs)
        {
            var inputPath = History.GetOutputPath(source.NodeId, source.Column);

            if (inputPath.Stages.Count > 0)
            {
                AddStage(inputPath, 0, null, expressionNode);
            }
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

        if (value is { ParsedExpression: { } parsed })
        {
            Expressions[node.Key] = PredicateText.From(parsed);
        }
        else if (value is { Expression: { } text })
        {
            Expressions[node.Key] = new PredicateText([new PredicateToken(PredicateTokenType.Unknown, text)]);
        }

        return node;
    }

    private void AddEvaluation(PlanNode expression, int? consumerNodeId, MemoryStructureNode parent)
    {
        var puller = Plan?.GetPuller(expression);

        var owner = puller is { IsStatement: false } && puller.NodeId == consumerNodeId
            ? FindOperatorNode(parent) ?? parent
            : CurrentColumn ?? parent;

        var definition = GetDefinition(expression);

        if (!owner.Children.Exists(c => c.Type == MemoryStructureNodeType.Expression && c.Name == definition))
        {
            owner.Children.Add(CreateExpression(expression, expression.DefinedValues is [var only] ? only : null, owner));
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

        var key = $"{node.Key} {string.Join(",", values.SelectMany(v => v))}";

        if (key != TableKey || EntryTable is null)
        {
            TableKey = key;

            var names = GetNodeId(node) is { } nodeId ? GetColumnNames(nodeId) : [];

            List<ResultColumn> columns =
            [
                new(0, EntryColumnName, typeof(string), false) { Width = EntryColumnWidth },
                .. fields.Select((f, i) => new ResultColumn(i + 1, f.Name, typeof(string), true)
                {
                    Width = EntryFieldWidth,
                    MarkerColour = colours[i]
                })
            ];

            var rows = entries.Select((e, i) => new ResultRow<long>([GetEntryName(e, names),
                                                                     .. fields.Select((f, j) => GetEntryCell(f, values[i][j]))])
            {
                Id = (long)e.Link.Address
            }).ToList();

            EntryTable = new QueryResultSet { Columns = columns, Rows = rows };
        }

        SelectItemRow();

        ShowItemMarkers($"{node.Key} entries",
                        () => offsets.SelectMany(o => fields.Select((f, i) => CreateMarker(f.Name, o + f.Offset, f.Length, colours[i]))),
                        size);

        return SelectedItem is { } selected && selected >= start ? (int)(selected - start) : null;
    }

    private object? GetEntryCell(MemoryField field, ulong? value)
        => field.Name == TypeFieldName && value is { } type && SqlTypeHelpers.TryGetSqlType((byte)type, out var sqlType)
            ? SqlDataTypeFormat.GetName(sqlType)
            : GetValueCell(field, value);

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

    private static string GetDefinition(PlanNode expression) => string.Join(", ", DataPathPlan.GetDefinitions(expression));

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

    private PlanNode? GetExpression(PlanOutputPath path, int stageIndex, int? consumerNodeId)
    {
        for (var index = stageIndex + 1; index < path.Stages.Count; index++)
        {
            if (path.Stages[index].Consumer == stageIndex && FindOperator(path.Stages[index]) is { } inputNodeId)
            {
                return Plan?.GetExpression(consumerNodeId, inputNodeId);
            }
        }

        return null;
    }

    private PlanOutputRouteNode? GetHeld(DataPathInput input)
        => Routes.GetValueOrDefault(input.NodeId)?.Columns.ElementAtOrDefault(GetColumnIndex(input.NodeId, input.Column.Reference)).Node;

    private List<DataPathColumn> GetTopColumns(IReadOnlyList<int> tops)
    {
        var columns = new List<DataPathColumn>();

        foreach (var top in tops)
        {
            var outputs = History.GetOutputColumns(top);

            var names = GetColumnNames(top);

            for (var index = 0; index < outputs.Count; index++)
            {
                columns.Add(new DataPathColumn(index < names.Count ? names[index] : string.Empty,
                                               outputs[index].Type,
                                               [new DataPathInput(top, outputs[index])]));
            }
        }

        return columns;
    }

    private PlanOutputColumn? GetOutputColumn(int nodeId, int index)
        => History.GetOutputColumns(nodeId) is var outputs && index < outputs.Count ? outputs[index] : null;

    private void AddStage(PlanOutputPath path, int stageIndex, int? consumerNodeId, MemoryStructureNode parent)
    {
        var stage = path.Stages[stageIndex];

        if (FindOperator(stage) is not { } nodeId)
        {
            if (GetExpression(path, stageIndex, consumerNodeId) is { } expression)
            {
                AddEvaluation(expression, consumerNodeId, parent);

                AddInputs(path, stageIndex, consumerNodeId ?? expression.NodeId, parent);
            }

            return;
        }

        if (nodeId == consumerNodeId)
        {
            AddInputs(path, stageIndex, nodeId, parent);

            return;
        }

        var prefix = parent.Type == MemoryStructureNodeType.Expression ? GetColumnName(nodeId, stage.Reference) : null;

        var operatorNode = CreateOperator(nodeId, parent, prefix);

        ValueNodes.Add((stage, operatorNode));

        if (PlanOutputLinkText.GetReferenceName(stage) is { Length: > 0 } reference)
        {
            operatorNode.Badges.Add(CreateBadge(reference));
        }

        parent.Children.Add(operatorNode);

        var names = GetColumnNames(nodeId);

        var column = GetColumnIndex(nodeId, stage.Reference);

        var route = Routes.GetValueOrDefault(nodeId);

        if (route?.Columns.ElementAtOrDefault(column) is { Node: { } held } location && location.NodeId == nodeId)
        {
            operatorNode.Children.Add(CreateObject(GetDisplayRoot(held), operatorNode, names));

            if (FindBinding(route, held) is { } binding)
            {
                operatorNode.Children.Add(CreateObject(binding, operatorNode, names));
            }
        }

        if (route is not null && IsPlanObjectVisible && Flatten(route.FromPlanObject).FirstOrDefault(IsReferenceTo(column)) is { } compiled)
        {
            operatorNode.Children.Add(CreateObject(compiled, operatorNode, names));
        }

        AddInputs(path, stageIndex, nodeId, operatorNode);
    }

    private void AddInputs(PlanOutputPath path, int stageIndex, int nodeId, MemoryStructureNode operatorNode)
    {
        var names = GetColumnNames(nodeId);

        for (var index = stageIndex + 1; index < path.Stages.Count; index++)
        {
            if (path.Stages[index].Consumer != stageIndex)
            {
                continue;
            }

            var input = path.Stages[index];

            MemoryStructureNode? holder = null;

            foreach (var reader in GetInputReaders(input, nodeId))
            {
                var readerNode = FindRouteNode(operatorNode, reader);

                if (readerNode is null)
                {
                    readerNode = CreateObject(reader, operatorNode, names);

                    operatorNode.Children.Add(readerNode);
                }

                holder ??= readerNode;
            }

            AddStage(path, index, nodeId, holder ?? FindReader(operatorNode, input) ?? operatorNode);
        }
    }

    private MemoryStructureNode? FindReader(MemoryStructureNode operatorNode, PlanOutputStage input)
        => FindOperator(input) is { } inputNodeId
            ? operatorNode.Children
                          .SelectMany(Flatten)
                          .FirstOrDefault(n => RouteNodes.TryGetValue(n, out var route) && IsReading(route, inputNodeId))
            : null;

    private MemoryStructureNode CreateOperator(int nodeId, MemoryStructureNode parent, string? prefix)
    {
        var found = Operators[nodeId];

        return new MemoryStructureNode(MemoryStructureNodeType.Operator,
                                       prefix is { Length: > 0 } ? $"{prefix} › {found.Event.Label}" : found.Event.Label,
                                       found.Iterator != 0 ? found.Iterator : found.PlanObject,
                                       -1,
                                       parent)
        {
            Index = NodeCount++,
            IsAddressVisible = false
        };
    }

    private MemoryStructureNode CreateObject(PlanOutputRouteNode route, MemoryStructureNode parent, IReadOnlyList<string> names)
        => CreateLink(route, parent, names, Containers.TryGetValue(route, out var container) ? GetDisplayName(container) : null);

    private List<PlanOutputRouteNode> GetInputReaders(PlanOutputStage input, int nodeId)
    {
        if (FindOperator(input) is not { } inputNodeId
            || Routes.GetValueOrDefault(nodeId) is not { } route
            || Routes.GetValueOrDefault(inputNodeId)?.Columns.ElementAtOrDefault(GetColumnIndex(inputNodeId, input.Reference)) is not
                { Node: { } held })
        {
            return [];
        }

        var targets = GetContainers(held).ToHashSet();

        var nodes = Flatten(route.FromIterator).ToList();

        var inputs = nodes.Where(n => n.Link.Type == PlanOutputLinkType.InputValue).ToList();

        return [.. nodes.Where(n => n.Targets.Exists(t => t.NodeId == inputNodeId && t.Node is { } target && targets.Contains(target)))
                        .Select(n => GetContainers(n).Select(c => inputs.Find(i => i.Link.Address == c.Link.Address))
                                                     .FirstOrDefault(i => i is not null))
                        .OfType<PlanOutputRouteNode>()
                        .Distinct()];
    }

    private IEnumerable<PlanOutputRouteNode> GetContainers(PlanOutputRouteNode node)
    {
        for (var current = node; current is not null; current = Containers.GetValueOrDefault(current))
        {
            yield return current;
        }
    }

    private static bool IsReading(PlanOutputRouteNode route, int nodeId) => route.Targets.Exists(t => t.NodeId == nodeId);

    private static MemoryStructureBadge CreateBadge(string reference)
        => PlanOutputLinkText.GetReferenceColour(reference) is { } colour
            ? new MemoryStructureBadge(reference, colour) { TextColour = Colors.White }
            : new MemoryStructureBadge(reference, ReferenceColour);

    private static string? GetSide(MemoryField field)
    {
        if (field == EngineLayout.HashIterator.BuildInputValuesField)
        {
            return InputLabel.Build;
        }

        return field == EngineLayout.HashIterator.ProbeInputValuesField ? InputLabel.Probe : null;
    }

    private string? GetColumnName(int nodeId, ulong reference)
        => GetColumnIndex(nodeId, reference) is >= 0 and var index && GetColumnNames(nodeId) is var names && index < names.Count
            ? names[index]
            : null;

    private int? FindOperator(PlanOutputStage stage)
    {
        foreach (var (nodeId, found) in Operators)
        {
            if (stage.Iterator != 0 && found.Iterator == stage.Iterator || stage.PlanObject != 0 && found.PlanObject == stage.PlanObject)
            {
                return nodeId;
            }
        }

        return null;
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

    private PlanOutputRouteOperator GetRouteOperator(int nodeId, DataPathOperator found)
    {
        var history = History;

        var paths = history.GetOutputColumns(nodeId).Select(c => history.GetOutputPath(nodeId, c)).ToList();

        return new PlanOutputRouteOperator(nodeId, found.PlanObject, found.Iterator, paths);
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
