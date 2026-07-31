using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// A nested loops join whose inner side seeks on values bound from the outer side's rows
/// </summary>
public sealed record CorrelatedJoin(PlanNode Join, PlanNode Outer, PlanNode Inner, JoinType JoinType);
