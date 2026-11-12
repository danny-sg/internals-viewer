using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public sealed class PlanOutputRoute
{
    public const int StatementNodeId = -1;

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
        PlanOutputLinkType.Input,
        PlanOutputLinkType.Expression,
        PlanOutputLinkType.PlanObject,
        PlanOutputLinkType.GroupColumns,
        PlanOutputLinkType.AggregateColumns,
        PlanOutputLinkType.CountColumns
    ];

    private static readonly HashSet<PlanOutputLinkType> WiringTypes = [PlanOutputLinkType.Inputs, PlanOutputLinkType.ProfilingIterator];

    private static readonly HashSet<PlanOutputLinkType> ReferenceTypes = [PlanOutputLinkType.Reference, PlanOutputLinkType.Input];

    private static readonly HashSet<PlanOutputLinkType> SharedTypes = [PlanOutputLinkType.RowContext];

    private static readonly HashSet<PlanOutputLinkType> OutputTypes =
    [
        PlanOutputLinkType.Value,
        PlanOutputLinkType.InputValue,
        PlanOutputLinkType.BufferEntry,
        PlanOutputLinkType.Variables
    ];

    private static readonly HashSet<PlanOutputLinkType> ValueArrayTypes =
    [
        PlanOutputLinkType.OutputValues,
        PlanOutputLinkType.RowBuffer,
        PlanOutputLinkType.Variables
    ];

    private static readonly HashSet<PlanOutputLinkType> ArrayTypes =
    [
        PlanOutputLinkType.OutputArray,
        PlanOutputLinkType.Inputs,
        PlanOutputLinkType.OutputValues,
        PlanOutputLinkType.InputValues,
        PlanOutputLinkType.RowContext,
        PlanOutputLinkType.RowBuffer,
        PlanOutputLinkType.VariableTable,
        PlanOutputLinkType.Variables,
        PlanOutputLinkType.GroupColumns,
        PlanOutputLinkType.AggregateColumns,
        PlanOutputLinkType.CountColumns,
        PlanOutputLinkType.ColumnBindings
    ];

    public List<PlanOutputRouteNode> FromPlanObject { get; } = [];

    public List<PlanOutputRouteNode> FromIterator { get; } = [];

    public static Dictionary<int, PlanOutputRoute> Create(IReadOnlyList<PlanOutputRouteOperator> operators, bool isPlanObjectIncluded)
    {
        var routes = new Dictionary<int, PlanOutputRoute>();

        var iterators = new Dictionary<ulong, int>();

        foreach (var found in operators)
        {
            routes.TryAdd(found.NodeId, new PlanOutputRoute());

            if (found.Iterator != 0)
            {
                iterators.TryAdd(found.Iterator, found.NodeId);
            }
        }

        var owners = new Dictionary<ulong, PlanOutputRouteEdge>();

        foreach (var found in operators.OrderByDescending(o => GetDepth(operators, o)).ThenBy(o => o.NodeId))
        {
            for (var column = 0; column < found.Paths.Count; column++)
            {
                routes[found.NodeId].Add(found, found.Paths[column], column, iterators, owners, isPlanObjectIncluded);
            }
        }

        foreach (var found in operators.Where(o => IsRoot(operators, o)))
        {
            foreach (var path in found.Paths)
            {
                if (GetOutputAddress(path) is { } address && owners.TryGetValue(address, out var owner))
                {
                    AddReader(owner, new PlanOutputRouteEdge(StatementNodeId, null, owner.Link));
                }
            }
        }

        return routes;
    }

    private static int GetDepth(IReadOnlyList<PlanOutputRouteOperator> operators, PlanOutputRouteOperator found)
    {
        var depth = 0;

        var parent = found.ParentNodeId;

        while (depth < operators.Count && parent is { } nodeId && operators.FirstOrDefault(o => o.NodeId == nodeId) is { } next)
        {
            depth++;

            parent = next.ParentNodeId;
        }

        return depth;
    }

    private static bool IsRoot(IReadOnlyList<PlanOutputRouteOperator> operators, PlanOutputRouteOperator found)
        => found.ParentNodeId is not { } parent || operators.All(o => o.NodeId != parent);

    private static ulong? GetOutputAddress(PlanOutputPath path)
    {
        foreach (var link in path.Links)
        {
            if (link is { IsRoute: false, Depth: 0 } && OutputTypes.Contains(link.Type))
            {
                return link.Address;
            }
        }

        return path.Stages.Count > 0 ? path.Stages[0].Value?.Address : null;
    }

    private static void AddReader(PlanOutputRouteEdge owner, PlanOutputRouteEdge reader)
    {
        var readers = owner.Node!.Readers;

        if (!readers.Exists(r => r.NodeId == reader.NodeId && r.Link.Source == reader.Link.Source))
        {
            readers.Add(reader);
        }
    }

    private static void AddTarget(PlanOutputRouteNode? node, PlanOutputRouteEdge target)
    {
        if (node is not null && !node.Targets.Exists(t => t.Node == target.Node))
        {
            node.Targets.Add(target);
        }
    }

    private static void Claim(Dictionary<ulong, PlanOutputRouteEdge> owners, int nodeId, PlanOutputRouteNode node)
    {
        var link = node.Link;

        if (SharedTypes.Contains(link.Type))
        {
            return;
        }

        if (!owners.TryGetValue(link.Address, out var owner)
            || owner.NodeId == nodeId && ArrayTypes.Contains(owner.Link.Type) && !ArrayTypes.Contains(link.Type))
        {
            owners[link.Address] = new PlanOutputRouteEdge(nodeId, node, link);
        }
    }

    private static bool IsContext(PlanOutputLink link)
        => ContextTypes.Contains(link.Type) || link is { Type: PlanOutputLinkType.RowContext, IsRoute: true };

    private static bool IsOtherIterator(PlanOutputRouteNode? node, PlanOutputRouteOperator source, Dictionary<ulong, int> iterators)
        => node is { Link.Type: PlanOutputLinkType.Iterator }
           && node.Link.Address != source.Iterator
           && iterators.ContainsKey(node.Link.Address);

    private static bool IsDropped(PlanOutputLink link,
                                  PlanOutputRouteNode? holder,
                                  PlanOutputRouteOperator source,
                                  Dictionary<ulong, int> iterators,
                                  HashSet<ulong> dropped,
                                  bool isPlanObjectIncluded)
        => !isPlanObjectIncluded && CompiledTypes.Contains(link.Type)
           || WiringTypes.Contains(link.Type)
           || IsOtherIterator(holder, source, iterators)
           || holder is null && link.Source != source.Iterator && dropped.Contains(link.Source);

    private static int FindHolder(IReadOnlyList<PlanOutputLink> links, PlanOutputRouteNode?[] nodes, int index)
    {
        for (var holder = index - 1; holder >= 0; holder--)
        {
            if (nodes[holder] is not null && links[holder].Address == links[index].Source)
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

    private static PlanOutputRouteNode GetOrAdd(List<PlanOutputRouteNode> siblings, PlanOutputLink link, int index, int column)
    {
        var node = siblings.Find(n => n.Link.Type == link.Type && n.Link.Address == link.Address && n.Index == index);

        if (node is null)
        {
            node = new PlanOutputRouteNode(link, index, column);

            siblings.Add(node);
        }

        return node;
    }

    private List<PlanOutputRouteNode> GetSiblings(PlanOutputLink link,
                                                  PlanOutputRouteNode? holder,
                                                  PlanOutputRouteNode? previous,
                                                  PlanOutputRouteOperator source)
    {
        if (holder is { Link.Type: PlanOutputLinkType.Iterator } && holder.Link.Address == source.Iterator)
        {
            return FromIterator;
        }

        if (holder is not null)
        {
            return holder.Children;
        }

        if (link.Source == source.Iterator)
        {
            return FromIterator;
        }

        return link.Source == source.PlanObject || previous is null ? FromPlanObject : previous.Children;
    }

    private void Add(PlanOutputRouteOperator source,
                     PlanOutputPath path,
                     int column,
                     Dictionary<ulong, int> iterators,
                     Dictionary<ulong, PlanOutputRouteEdge> owners,
                     bool isPlanObjectIncluded)
    {
        var links = path.Links;

        var nodes = new PlanOutputRouteNode?[links.Count];

        var dropped = new HashSet<ulong>();

        PlanOutputRouteNode? previous = null;

        var stage = 0;

        var isColumnPlaced = isPlanObjectIncluded;

        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];

            if (IsContext(link))
            {
                continue;
            }

            var stageIndex = ReferenceTypes.Contains(link.Type) ? stage++ : -1;

            var holder = FindHolder(links, nodes, index);

            var holderNode = holder >= 0 ? nodes[holder] : null;

            if (IsDropped(link, holderNode, source, iterators, dropped, isPlanObjectIncluded))
            {
                dropped.Add(link.Address);

                continue;
            }

            if (!link.IsRoute && owners.TryGetValue(link.Address, out var owner) && owner.NodeId != source.NodeId)
            {
                AddReader(owner, new PlanOutputRouteEdge(source.NodeId, holderNode, link));

                AddTarget(holderNode, owner with { Link = link });

                dropped.Add(link.Address);

                continue;
            }

            var entry = holder >= 0 && ArrayTypes.Contains(links[holder].Type) ? GetIndex(link) : -1;

            var entryColumn = entry >= 0 && links[holder].Type == PlanOutputLinkType.OutputArray && link.Depth == 0
                              || link is { Type: PlanOutputLinkType.ColumnBinding, Depth: 0 }
                ? column
                : -1;

            if (!isColumnPlaced
                && link.Depth == 0
                && (entry >= 0 && ValueArrayTypes.Contains(links[holder].Type)
                    || link.Type is PlanOutputLinkType.BufferEntry or PlanOutputLinkType.Record))
            {
                entryColumn = column;

                isColumnPlaced = true;
            }

            var node = GetOrAdd(GetSiblings(link, holderNode, previous, source), link, entry, entryColumn);

            nodes[index] = node;

            previous = node;

            if (!link.IsRoute)
            {
                Claim(owners, source.NodeId, node);
            }
            else if (stageIndex >= 0
                     && stageIndex < path.Stages.Count
                     && path.Stages[stageIndex] is { } found
                     && found.PlanObject != source.PlanObject
                     && found.Value?.Address is { } address
                     && owners.TryGetValue(address, out var producer)
                     && producer.NodeId != source.NodeId)
            {
                AddReader(producer, new PlanOutputRouteEdge(source.NodeId, node, link));

                AddTarget(node, producer with { Link = link });
            }
        }
    }
}
