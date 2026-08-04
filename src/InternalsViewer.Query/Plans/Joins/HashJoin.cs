using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// A hash match whose sides are both direct data accesses, the first building the table and the second probing it
/// </summary>
public sealed record HashJoin(PlanNode Join, PlanNode Build, PlanNode Probe, JoinType JoinType);
