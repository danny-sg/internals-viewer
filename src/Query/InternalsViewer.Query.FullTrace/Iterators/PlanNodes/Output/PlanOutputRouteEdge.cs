namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public readonly record struct PlanOutputRouteEdge(int NodeId, PlanOutputRouteNode? Node, PlanOutputLink Link);
