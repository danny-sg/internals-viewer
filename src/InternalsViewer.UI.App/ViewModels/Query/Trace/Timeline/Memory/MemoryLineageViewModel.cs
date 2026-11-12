using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed class MemoryLineageViewModel : MemoryStructureViewModel
{
    private const string LineageTitle = "Lineage";

    private const string PlanObjectName = "Plan Object";

    private const string NoOperators = "No Operators";

    private const string RecordOffsetPrefix = "Record Offset ";

    private const string VariableColumnPrefix = "Variable Column ";

    public MemoryLineageViewModel(MemoryHistoryViewModel history, TracePosition position, ITraceNavigator navigator)
        : base(history, position, navigator)
    {
        Loader = new PlayheadLoader<HexImage>(LoadInterval, Apply);

        IsApplying = true;

        BuildTree(null);

        IsApplying = false;

        Show();
    }

    public override string Title => LineageTitle;

    public override string Key => LineageTitle;

    protected override HexImage? Image => Loaded;

    private PlayheadLoader<HexImage> Loader { get; }

    private HexImage? Loaded { get; set; }

    private Dictionary<string, List<MemoryField>> LinkFields { get; } = [];

    private string? ScrolledKey { get; set; }

    private int NodeCount { get; set; }

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

        Loaded = null;

        base.Close();
    }

    protected override void Load()
    {
        var history = History;

        var address = HexAddress;

        var position = Position;

        Loader.Request(() => HexImage.Read(history, address, position));
    }

    protected override void ShowDetail(MemoryStructureNode node)
    {
        var fields = GetFields(node);

        ShowFields(node, fields, CreateColours(fields.Count));

        if (node.Key != ScrolledKey && Image is { } image)
        {
            ScrolledKey = node.Key;

            ScrollToOffset = null;

            ScrollToOffset = image.Offset;
        }
    }

    protected override ResultDetails GetEntryDetails(ResultRow<long> row) => new(string.Empty, []);

    protected override MemoryStructureNode? GetPointerNode(ulong value) => Nodes.GetValueOrDefault(value);

    protected override void OpenPointer(MemoryStructureNode node, ulong value) => Select(node);

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

        var operators = history.IteratorTargets
                               .Where(t => t.Value.Operator?.PlanNodeIdentifier is not null)
                               .GroupBy(t => t.Value.Operator!.PlanNodeIdentifier!.NodeId)
                               .ToDictionary(g => g.Key,
                                             g => new LineageOperator(g.First().Value.Operator!,
                                                                      GetTarget(g, IteratorTargetType.Iterator),
                                                                      GetTarget(g, IteratorTargetType.PlanObject)));

        var tops = operators.Where(o => o.Value.Event.ParentNodeId is not { } parent || !operators.ContainsKey(parent))
                            .OrderBy(o => o.Key)
                            .ToList();

        if (tops.Count == 0)
        {
            EmptyText = NoOperators;

            return;
        }

        var statement = tops[0].Value.Event.PlanNodeIdentifier is { } identifier
                        && history.GetStatementName(identifier) is { } name
            ? Node(MemoryStructureNodeType.Statement, name, 0, null)
            : null;

        var roots = new List<MemoryStructureNode>();

        foreach (var top in tops)
        {
            var node = CreateOperator(operators, top.Key, statement);

            (statement?.Children ?? roots).Add(node);
        }

        if (statement is not null)
        {
            roots.Add(statement);
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

    private MemoryStructureNode CreateOperator(Dictionary<int, LineageOperator> operators, int nodeId, MemoryStructureNode? parent)
    {
        var history = History;

        var found = operators[nodeId];

        var node = Node(MemoryStructureNodeType.Operator, found.Event.Label, found.Iterator, parent);

        var holder = node;

        if (IsPlanObjectVisible && found.PlanObject != 0)
        {
            holder = Node(MemoryStructureNodeType.PlanObject, PlanObjectName, found.PlanObject, node);

            node.Children.Add(holder);
        }

        var columns = history.GetOutputColumns(nodeId);

        var names = history.GetOutputColumnNames(found.Iterator != 0 ? found.Iterator : found.PlanObject, columns.Count);

        var route = PlanOutputRoute.Create([.. columns.Select(c => history.GetOutputPath(nodeId, c))],
                                           found.PlanObject,
                                           found.Iterator,
                                           IsPlanObjectVisible);

        foreach (var link in route.FromPlanObject)
        {
            holder.Children.Add(CreateLink(link, holder, names));
        }

        foreach (var link in route.FromIterator)
        {
            node.Children.Add(CreateLink(link, node, names));
        }

        foreach (var child in operators.Where(o => o.Value.Event.ParentNodeId == nodeId).OrderBy(o => o.Key))
        {
            node.Children.Add(CreateOperator(operators, child.Key, node));
        }

        return node;
    }

    private MemoryStructureNode CreateLink(PlanOutputRouteNode route, MemoryStructureNode parent, IReadOnlyList<string> names)
    {
        var link = route.Link;

        var name = GetName(link);

        var node = new MemoryStructureNode(MemoryStructureNodeType.Link,
                                           route.Index >= 0 ? $"{name} [{route.Index}]" : name,
                                           link.Address,
                                           -1,
                                           parent)
        {
            Index = NodeCount++,
            Detail = route.Column >= 0 && route.Column < names.Count ? names[route.Column] : GetRecordOffsetText(link)
        };

        var fields = new List<MemoryField>();

        foreach (var child in route.Children)
        {
            node.Children.Add(CreateLink(child, node, names));

            fields.AddRange(GetChildFields(child, link.Address));
        }

        if (fields.Count == 0)
        {
            fields.AddRange(GetOwnFields(link));
        }

        LinkFields[node.Key] = [.. fields.DistinctBy(f => f.Offset).OrderBy(f => f.Offset)];

        return node;
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

        return link.Field is { } field && link.Address == link.Source + (ulong)link.SourceOffset ? [field with { Offset = 0 }] : [];
    }

    private IEnumerable<MemoryField> GetChildFields(PlanOutputRouteNode child, ulong address)
    {
        var link = child.Link;

        var block = History.GetBlock(address, link.Position);

        if (block < 0 || History.GetBlock(link.Source, link.Position) != block || link.Source + (ulong)link.SourceOffset < address)
        {
            return [];
        }

        var offset = (int)(link.Source + (ulong)link.SourceOffset - address);

        var name = link.Field?.Name ?? PlanOutputLinkText.GetName(link.Type);

        if (child.Index >= 0)
        {
            name = $"{name} [{child.Index}]";
        }

        if (IsEntryOf(link))
        {
            return GetEntryFields($"{name} ", offset);
        }

        return [link.Field is { } field ? field with { Name = name, Offset = offset } : new MemoryField(name, offset, sizeof(ulong))];
    }

    private string GetName(PlanOutputLink link)
    {
        var name = PlanOutputLinkText.GetName(link.Type);

        if (History.IteratorTargets.TryGetValue(link.Address, out var target))
        {
            return $"{name} › {target.Label}";
        }

        return link.Type == PlanOutputLinkType.RowBuffer && History.GetRowBufferOwner(link.Address, link.Position) is { } owner
            ? $"{name} › {owner.Label}"
            : name;
    }

    private static ulong GetTarget(IEnumerable<KeyValuePair<ulong, IteratorTarget>> targets, IteratorTargetType type)
        => targets.FirstOrDefault(t => t.Value.Type == type).Key;

    private MemoryStructureNode Node(MemoryStructureNodeType type, string name, ulong address, MemoryStructureNode? parent)
        => new(type, name, address, -1, parent) { Index = NodeCount++ };

    private sealed record LineageOperator(ExecutionOperatorEvent Event, ulong Iterator, ulong PlanObject);
}
