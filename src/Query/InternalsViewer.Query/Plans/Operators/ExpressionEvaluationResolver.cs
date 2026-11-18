using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Operators;

public static class ExpressionEvaluationResolver
{
    private const string ConstantPrefix = "ConstExpr";

    public static List<ExpressionEvaluation> Resolve(ExecutionPlan plan) => [.. plan.Root.SelectMany(Resolve)];

    public static List<ExpressionEvaluation> Resolve(PlanNode root)
    {
        var evaluations = new List<ExpressionEvaluation>();

        Visit(root, [], evaluations);

        return evaluations;
    }

    private static void Visit(PlanNode node, List<PlanNode> ancestors, List<ExpressionEvaluation> evaluations)
    {
        if (OperatorClassifier.IsComputeScalar(node) && !node.IsBatchMode)
        {
            AddEvaluations(node, ancestors, evaluations);
        }

        ancestors.Add(node);

        foreach (var child in node.Children)
        {
            Visit(child, ancestors, evaluations);
        }

        ancestors.RemoveAt(ancestors.Count - 1);
    }

    private static void AddEvaluations(PlanNode computeScalar, List<PlanNode> ancestors, List<ExpressionEvaluation> evaluations)
    {
        var sites = new Dictionary<(PlanNode Evaluator, ExpressionEvaluationSite Site), 
                                   (List<DefinedValueInfo> All, List<DefinedValueInfo> Output)>();

        foreach (var value in computeScalar.DefinedValues)
        {
            foreach (var (evaluator, site, isOutput) in GetEvaluators(computeScalar, value, ancestors).Distinct())
            {
                if (!sites.TryGetValue((evaluator, site), out var expressions))
                {
                    expressions = ([], []);

                    sites[(evaluator, site)] = expressions;
                }

                expressions.All.Add(value);

                if (isOutput)
                {
                    expressions.Output.Add(value);
                }
            }
        }

        evaluations.AddRange(sites.Select(s => new ExpressionEvaluation(computeScalar, 
                                                                        s.Key.Evaluator, 
                                                                        s.Key.Site, 
                                                                        s.Value.All, 
                                                                        s.Value.Output)));
    }

    private static IEnumerable<(PlanNode Evaluator, ExpressionEvaluationSite Site, bool IsOutput)> 
        GetEvaluators(PlanNode computeScalar,
                      DefinedValueInfo value,
                      List<PlanNode> ancestors)
    {
        HashSet<string> names = [.. value.Columns.Select(c => c.Column)];

        if (IsConstant(value))
        {
            yield return (ancestors[0], ExpressionEvaluationSite.Startup, ancestors[0].Children.Any(c => IsCarried(c, names)));

            yield break;
        }

        var child = computeScalar;

        for (var index = ancestors.Count - 1; index >= 0; index--)
        {
            var parent = ancestors[index];

            if (parent.IsStatement)
            {
                if (IsCarried(child, names))
                {
                    yield return (parent, ExpressionEvaluationSite.FirstInput, true);
                }

                yield break;
            }

            if (OperatorClassifier.IsComputeScalar(parent))
            {
                var derived = parent.DefinedValues.Where(d => d.References.Any(r => names.Contains(r.Column))).ToList();

                names.UnionWith(derived.SelectMany(d => d.Columns.Select(c => c.Column)));

                child = parent;

                continue;
            }

            var input = parent.Children.IndexOf(child);

            if (OperatorClassifier.IsNestedLoop(parent) && input == 0 && parent.OuterReferences.Overlaps(names))
            {
                foreach (var inner in GetDescendants(parent.Children[1]).Where(n => IsUsed(n, names)))
                {
                    yield return (inner, ExpressionEvaluationSite.FirstInput, false);
                }
            }

            var isCarried = IsCarried(parent, names);

            var isStored = isCarried && IsStored(parent, input);

            if (isStored || IsUsed(parent, names))
            {
                yield return (parent, input == 1 ? ExpressionEvaluationSite.SecondInput : ExpressionEvaluationSite.FirstInput, isStored);
            }

            if (isStored || !isCarried)
            {
                yield break;
            }

            child = parent;
        }
    }

    private static bool IsConstant(DefinedValueInfo value)
        => value.References.Count > 0 && value.References.All(r => r.Column.StartsWith(ConstantPrefix, StringComparison.Ordinal));

    private static bool IsUsed(PlanNode node, HashSet<string> names)
        => node.ReferencedColumns.Exists(c => c.Column.Table.Length == 0 && names.Contains(c.Column.Column));

    private static bool IsCarried(PlanNode node, HashSet<string> names) => node.OutputColumns.Any(c => names.Contains(c.Column));

    private static bool IsStored(PlanNode node, int input)
        => OperatorClassifier.IsSort(node) || OperatorClassifier.IsSpool(node) || (OperatorClassifier.IsHash(node) && input == 0);

    private static IEnumerable<PlanNode> GetDescendants(PlanNode node) => node.Children.SelectMany(GetDescendants).Prepend(node);
}
