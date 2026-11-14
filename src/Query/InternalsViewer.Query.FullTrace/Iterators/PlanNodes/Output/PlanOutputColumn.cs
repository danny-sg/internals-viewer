using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public readonly record struct PlanOutputColumn(ulong Reference, ulong Producer, SqlTypeInfo Type)
{
    public int ProducerNodeId { get; init; } = -1;
}
