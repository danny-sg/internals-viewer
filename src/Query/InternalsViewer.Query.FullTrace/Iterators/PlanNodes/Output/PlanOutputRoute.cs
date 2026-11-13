using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public sealed class PlanOutputRoute
{
    private const int PointerSize = sizeof(ulong);

    private static readonly HashSet<PlanOutputLinkType> ContextTypes =
    [
        PlanOutputLinkType.ExecutionContext,
        PlanOutputLinkType.QueryContext,
        PlanOutputLinkType.IteratorMap,
        PlanOutputLinkType.QueryScan,
        PlanOutputLinkType.QueryScanContext,
        PlanOutputLinkType.Statement
    ];

    private static readonly HashSet<PlanOutputLinkType> CompiledTypes =
    [
        PlanOutputLinkType.OutputList,
        PlanOutputLinkType.OutputArray,
        PlanOutputLinkType.Reference,
        PlanOutputLinkType.Producer,
        PlanOutputLinkType.MapEntry,
        PlanOutputLinkType.Iterator
    ];

    private static readonly HashSet<PlanOutputLinkType> HolderTypes =
    [
        PlanOutputLinkType.Value,
        PlanOutputLinkType.InputValue,
        PlanOutputLinkType.Reader,
        PlanOutputLinkType.RowSlot
    ];

    private static readonly HashSet<PlanOutputLinkType> PointedTypes =
    [
        PlanOutputLinkType.BufferEntry,
        PlanOutputLinkType.Rowset,
        PlanOutputLinkType.Row,
        PlanOutputLinkType.Record
    ];

    private static readonly HashSet<PlanOutputLinkType> BlockTypes =
    [
        PlanOutputLinkType.Rowset,
        PlanOutputLinkType.Row,
        PlanOutputLinkType.Record
    ];

    private static readonly HashSet<PlanOutputLinkType> NamedTypes =
    [
        PlanOutputLinkType.Value,
        PlanOutputLinkType.InputValue,
        PlanOutputLinkType.BufferEntry,
        PlanOutputLinkType.Record
    ];

    private static readonly HashSet<PlanOutputLinkType> ArrayTypes =
    [
        PlanOutputLinkType.OutputArray,
        PlanOutputLinkType.OutputValues,
        PlanOutputLinkType.InputValues,
        PlanOutputLinkType.RowContext,
        PlanOutputLinkType.RowBuffer,
        PlanOutputLinkType.VariableTable,
        PlanOutputLinkType.Variables,
        PlanOutputLinkType.ColumnBindings,
        PlanOutputLinkType.Executors
    ];

    public List<PlanOutputRouteNode> FromPlanObject { get; } = [];

    public List<PlanOutputRouteNode> FromIterator { get; } = [];

    public List<PlanOutputRouteEdge> Columns { get; } = [];

    private List<(PlanOutputRouteNode? Holder, int Index)> Pending { get; } = [];

    public static Dictionary<int, PlanOutputRoute> Create(IReadOnlyList<PlanOutputRouteOperator> operators,
                                                          IReadOnlyDictionary<int, IReadOnlyList<PlanOutputLink>> storage,
                                                          Func<PlanOutputLink, ulong> getBlockStart,
                                                          bool isPlanObjectIncluded)
    {
        var routes = new Dictionary<int, PlanOutputRoute>();

        var owners = new Dictionary<ulong, PlanOutputRouteEdge>();

        var blocks = new Dictionary<ulong, PlanOutputRouteEdge>();

        foreach (var found in operators)
        {
            if (routes.TryAdd(found.NodeId, new PlanOutputRoute()))
            {
                routes[found.NodeId].AddStorage(found, storage.GetValueOrDefault(found.NodeId) ?? [], owners, blocks, getBlockStart);
            }
        }

        foreach (var found in operators)
        {
            routes[found.NodeId].AddReferences(found, storage.GetValueOrDefault(found.NodeId) ?? [], owners, blocks, getBlockStart);

            routes[found.NodeId].Name(found, owners);

            if (isPlanObjectIncluded)
            {
                routes[found.NodeId].AddCompiled(found, owners);
            }
        }

        return routes;
    }

    private static PlanOutputRouteEdge? FindOwner(PlanOutputLink link,
                                                  Dictionary<ulong, PlanOutputRouteEdge> owners,
                                                  Dictionary<ulong, PlanOutputRouteEdge> blocks,
                                                  Func<PlanOutputLink, ulong> getBlockStart)
    {
        if (owners.TryGetValue(link.Address, out var owner))
        {
            return owner;
        }

        return BlockTypes.Contains(link.Type) && getBlockStart(link) is not 0 and var start && blocks.TryGetValue(start, out var block)
            ? block
            : null;
    }

    private static void AddTarget(PlanOutputRouteNode? node, PlanOutputRouteEdge target)
    {
        if (node is not null && !node.Targets.Exists(t => t.Node == target.Node))
        {
            node.Targets.Add(target);
        }
    }

    private static void Claim(Dictionary<ulong, PlanOutputRouteEdge> owners,
                              Dictionary<ulong, PlanOutputRouteEdge> blocks,
                              Func<PlanOutputLink, ulong> getBlockStart,
                              int nodeId,
                              PlanOutputRouteNode node)
    {
        var link = node.Link;

        if (link.Type == PlanOutputLinkType.RowContext)
        {
            return;
        }

        var edge = new PlanOutputRouteEdge(nodeId, node, link);

        if (!owners.TryGetValue(link.Address, out var owner)
            || owner.NodeId == nodeId && ArrayTypes.Contains(owner.Link.Type) && !ArrayTypes.Contains(link.Type))
        {
            owners[link.Address] = edge;
        }

        if (BlockTypes.Contains(link.Type) && getBlockStart(link) is not 0 and var start)
        {
            blocks.TryAdd(start, edge);
        }
    }

    private static bool IsContext(PlanOutputLink link)
        => ContextTypes.Contains(link.Type) || link is { Type: PlanOutputLinkType.RowContext, IsRoute: true };

    private static int FindHolder(IReadOnlyList<PlanOutputLink> links, IReadOnlyList<bool> isKnown, int index)
    {
        for (var holder = index - 1; holder >= 0; holder--)
        {
            if (isKnown[holder] && links[holder].Address == links[index].Source && links[holder].Source != links[index].Source)
            {
                return holder;
            }
        }

        return -1;
    }

    private static int GetIndex(PlanOutputLink link)
    {
        var offset = link.Field?.Offset ?? 0;

        var length = link.Field?.Length ?? PointerSize;

        return length > 0 && link.SourceOffset >= offset ? (link.SourceOffset - offset) / length : -1;
    }

    private static PlanOutputRouteNode GetOrAdd(List<PlanOutputRouteNode> siblings, PlanOutputLink link, int index)
    {
        var node = siblings.Find(n => n.Link.Type == link.Type && n.Link.Address == link.Address && n.Index == index);

        if (node is null)
        {
            node = new PlanOutputRouteNode(link, index, -1);

            siblings.Add(node);
        }

        return node;
    }

    private static IEnumerable<PlanOutputRouteNode> Flatten(IEnumerable<PlanOutputRouteNode> nodes)
        => nodes.SelectMany(n => Flatten(n.Children).Prepend(n));

    private void AddStorage(PlanOutputRouteOperator source,
                            IReadOnlyList<PlanOutputLink> links,
                            Dictionary<ulong, PlanOutputRouteEdge> owners,
                            Dictionary<ulong, PlanOutputRouteEdge> blocks,
                            Func<PlanOutputLink, ulong> getBlockStart)
    {
        var nodes = new PlanOutputRouteNode?[links.Count];

        var isKnown = new bool[links.Count];

        var isPending = new bool[links.Count];

        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];

            if (IsContext(link) || link.IsRoute)
            {
                continue;
            }

            var holder = FindHolder(links, isKnown, index);

            if (holder < 0 && link.Source != source.Iterator)
            {
                continue;
            }

            isKnown[index] = true;

            if (holder >= 0 && (isPending[holder] || HolderTypes.Contains(links[holder].Type) && PointedTypes.Contains(link.Type)))
            {
                isPending[index] = true;

                Pending.Add((nodes[holder], index));

                continue;
            }

            var siblings = holder >= 0 ? nodes[holder]!.Children : FromIterator;

            var entry = holder >= 0 && ArrayTypes.Contains(links[holder].Type) ? GetIndex(link) : -1;

            nodes[index] = GetOrAdd(siblings, link, entry);

            Claim(owners, blocks, getBlockStart, source.NodeId, nodes[index]!);
        }
    }

    private void AddReferences(PlanOutputRouteOperator source,
                               IReadOnlyList<PlanOutputLink> links,
                               Dictionary<ulong, PlanOutputRouteEdge> owners,
                               Dictionary<ulong, PlanOutputRouteEdge> blocks,
                               Func<PlanOutputLink, ulong> getBlockStart)
    {
        var placed = new Dictionary<ulong, PlanOutputRouteNode>();

        var dropped = new HashSet<ulong>();

        foreach (var (holder, index) in Pending)
        {
            var link = links[index];

            var parent = holder ?? placed.GetValueOrDefault(link.Source);

            if (dropped.Contains(link.Source) || parent is null)
            {
                dropped.Add(link.Address);

                continue;
            }

            if (FindOwner(link, owners, blocks, getBlockStart) is { Node: not null } owner && owner.NodeId != source.NodeId)
            {
                AddTarget(parent, owner with { Link = link });

                dropped.Add(link.Address);

                continue;
            }

            placed[link.Address] = GetOrAdd(parent.Children, link, -1);
        }

        Pending.Clear();
    }

    private static ulong? GetValueAddress(PlanOutputPath path)
    {
        foreach (var link in path.Links)
        {
            if (link is { IsRoute: false, Depth: 0 } && NamedTypes.Contains(link.Type))
            {
                return link.Address;
            }
        }

        return path.Stages.Count > 0 ? path.Stages[0].Value?.Address : null;
    }

    private void Name(PlanOutputRouteOperator source, Dictionary<ulong, PlanOutputRouteEdge> owners)
    {
        for (var column = 0; column < source.Paths.Count; column++)
        {
            var path = source.Paths[column];

            var owner = GetValueAddress(path) is { } address && owners.TryGetValue(address, out var found)
                ? found
                : new PlanOutputRouteEdge(source.NodeId, null, default);

            Columns.Add(owner);

            if (owner is { Node.Column: < 0 } && owner.NodeId == source.NodeId)
            {
                owner.Node!.Column = column;
            }
        }

        var nodes = Flatten(FromIterator).ToList();

        foreach (var binding in nodes.Where(n => n.Link.Type == PlanOutputLinkType.ColumnBinding))
        {
            if (nodes.Find(n => n.Link.Type == PlanOutputLinkType.BufferEntry
                                && n.Index == binding.Link.Slot
                                && n.Column >= 0) is { } entry)
            {
                binding.Column = entry.Column;
            }
        }
    }

    private void AddCompiled(PlanOutputRouteOperator source, Dictionary<ulong, PlanOutputRouteEdge> owners)
    {
        for (var column = 0; column < source.Paths.Count; column++)
        {
            var path = source.Paths[column];

            var links = path.Links;

            var nodes = new PlanOutputRouteNode?[links.Count];

            var isKnown = new bool[links.Count];

            PlanOutputRouteNode? previous = null;

            for (var index = 0; index < links.Count; index++)
            {
                var link = links[index];

                if (link.Depth != 0 || !link.IsRoute || !CompiledTypes.Contains(link.Type))
                {
                    continue;
                }

                var holder = FindHolder(links, isKnown, index);

                var siblings = holder >= 0 ? nodes[holder]!.Children
                               : link.Source == source.PlanObject || previous is null ? FromPlanObject
                               : previous.Children;

                var entry = holder >= 0 && ArrayTypes.Contains(links[holder].Type) ? GetIndex(link) : -1;

                var node = GetOrAdd(siblings, link, entry);

                isKnown[index] = true;

                nodes[index] = node;

                previous = node;

                if (link.Type != PlanOutputLinkType.Reference)
                {
                    if (link.Type == PlanOutputLinkType.Iterator)
                    {
                        break;
                    }

                    continue;
                }

                node.Column = column;

                if (path.Stages.Count > 0
                    && path.Stages[0] is { } stage
                    && stage.PlanObject != source.PlanObject
                    && stage.Value?.Address is { } address
                    && owners.TryGetValue(address, out var producer)
                    && producer.NodeId != source.NodeId)
                {
                    AddTarget(node, producer with { Link = link });
                }
            }
        }
    }
}
