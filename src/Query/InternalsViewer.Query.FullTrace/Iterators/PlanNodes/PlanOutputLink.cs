using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

public readonly record struct PlanOutputLink(PlanOutputLinkType Type, ulong Address, TracePosition Position)
{
    public int Slot { get; init; } = -1;

    public int? Offset { get; init; }

    public ulong Source { get; init; }

    public int SourceOffset { get; init; }

    public int Depth { get; init; }

    public bool IsRoute { get; init; }
}
