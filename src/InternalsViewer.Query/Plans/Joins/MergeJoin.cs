using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// A merge join whose sides are both direct data accesses walked in join key order
/// </summary>
public sealed record MergeJoin(PlanNode Join, PlanNode Outer, PlanNode Inner, JoinType JoinType);
