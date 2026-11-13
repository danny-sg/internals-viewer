namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public sealed record PlanOutputRouteOperator(int NodeId, ulong PlanObject, ulong Iterator, IReadOnlyList<PlanOutputPath> Paths);
