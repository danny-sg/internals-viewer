using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.Plans.Enums;

namespace InternalsViewer.Query.FullTrace.DataPath;

public sealed class DataPathLink(DataPathField source, bool isBlocking)
{
    public DataPathField Source { get; } = source;

    public bool IsBlocking { get; } = isBlocking;

    public ColumnUseType Use { get; internal set; }

    public IReadOnlyList<PlanOutputRouteNode>? Readers { get; internal set; }

    public bool IsSeen => Readers is not { Count: 0 };
}
