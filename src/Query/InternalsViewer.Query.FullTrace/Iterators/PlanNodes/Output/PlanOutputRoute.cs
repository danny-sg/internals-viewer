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
        PlanOutputLinkType.Input,
        PlanOutputLinkType.Expression,
        PlanOutputLinkType.PlanObject,
        PlanOutputLinkType.GroupColumns,
        PlanOutputLinkType.AggregateColumns,
        PlanOutputLinkType.CountColumns
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
        PlanOutputLinkType.CountColumns
    ];

    public List<PlanOutputRouteNode> FromPlanObject { get; } = [];

    public List<PlanOutputRouteNode> FromIterator { get; } = [];

    public static PlanOutputRoute Create(IReadOnlyList<PlanOutputPath> paths,
                                         ulong planObject,
                                         ulong iterator,
                                         bool isPlanObjectIncluded)
    {
        var route = new PlanOutputRoute();

        for (var column = 0; column < paths.Count; column++)
        {
            route.Add(paths[column].Links, column, planObject, iterator, isPlanObjectIncluded);
        }

        return route;
    }

    private static bool IsContext(PlanOutputLink link)
        => ContextTypes.Contains(link.Type) || link is { Type: PlanOutputLinkType.RowContext, IsRoute: true };

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

    private void Add(IReadOnlyList<PlanOutputLink> links, int column, ulong planObject, ulong iterator, bool isPlanObjectIncluded)
    {
        var nodes = new PlanOutputRouteNode?[links.Count];

        var dropped = new HashSet<ulong>();

        var previous = -1;

        var isColumnPlaced = isPlanObjectIncluded;

        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];

            if (IsContext(link))
            {
                continue;
            }

            var holder = FindHolder(links, nodes, index);

            if (!isPlanObjectIncluded
                && (CompiledTypes.Contains(link.Type) || holder < 0 && link.Source != iterator && dropped.Contains(link.Source)))
            {
                dropped.Add(link.Address);

                continue;
            }

            List<PlanOutputRouteNode> siblings;

            if (holder >= 0)
            {
                siblings = nodes[holder]!.Children;
            }
            else if (link.Source == iterator)
            {
                siblings = FromIterator;
            }
            else if (link.Source == planObject || previous < 0)
            {
                siblings = FromPlanObject;
            }
            else
            {
                siblings = nodes[previous]!.Children;
            }

            var entry = holder >= 0 && ArrayTypes.Contains(links[holder].Type) ? GetIndex(link) : -1;

            var entryColumn = entry >= 0 && links[holder].Type == PlanOutputLinkType.OutputArray && link.Depth == 0 ? column : -1;

            if (!isColumnPlaced
                && link.Depth == 0
                && (entry >= 0 && ValueArrayTypes.Contains(links[holder].Type)
                    || link.Type is PlanOutputLinkType.BufferEntry or PlanOutputLinkType.Record))
            {
                entryColumn = column;

                isColumnPlaced = true;
            }

            nodes[index] = GetOrAdd(siblings, link, entry, entryColumn);

            previous = index;
        }
    }
}
