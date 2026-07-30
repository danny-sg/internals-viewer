namespace InternalsViewer.Query.Parsing.Plans;

/// <summary>
/// A merge join whose sides are both direct data accesses walked in join key order
/// </summary>
public sealed record MergeJoin(PlanNode Join, PlanNode Outer, PlanNode Inner);
