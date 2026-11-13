namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public sealed class PlanOutputRouteNode(PlanOutputLink link, int index, int column)
{
    public PlanOutputLink Link { get; } = link;

    public int Index { get; } = index;

    public int Column { get; internal set; } = column;

    public List<PlanOutputRouteNode> Children { get; } = [];

    public List<PlanOutputRouteEdge> Targets { get; } = [];
}
