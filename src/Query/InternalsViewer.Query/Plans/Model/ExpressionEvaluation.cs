using InternalsViewer.Query.Plans.Enums;

namespace InternalsViewer.Query.Plans.Model;

public sealed record ExpressionEvaluation(PlanNode ComputeScalar,
                                          PlanNode Evaluator,
                                          ExpressionEvaluationSite Site,
                                          IReadOnlyList<DefinedValueInfo> Values,
                                          IReadOnlyList<DefinedValueInfo> OutputValues);
