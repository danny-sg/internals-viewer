namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public sealed record PlanOutputRouteOperator(int NodeId,
                                             int? ParentNodeId,
                                             ulong PlanObject,
                                             ulong Iterator,
                                             IReadOnlyList<PlanOutputPath> Paths);
