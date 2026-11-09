using InternalsViewer.Query.Plans.Enums;

namespace InternalsViewer.Query.Plans.Model;

public sealed record ExpressionPull(PlanNode ComputeScalar,
                                     PlanNode Puller,
                                     ExpressionPullSite Site,
                                     IReadOnlyList<string> Expressions,
                                     IReadOnlyList<string> OutputExpressions);
