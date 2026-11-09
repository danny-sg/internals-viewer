using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Operators;

public static class ExpressionPullResolver
{
    private const string ConstantPrefix = "ConstExpr";

    public static List<ExpressionPull> Resolve(ExecutionPlan plan)
    {
        var pulls = new List<ExpressionPull>();

        foreach (var root in plan.Root)
        {
            Visit(root, [], pulls);
        }

        return pulls;
    }

    private static void Visit(PlanNode node, List<PlanNode> ancestors, List<ExpressionPull> pulls)
    {
        if (OperatorClassifier.IsComputeScalar(node) && !node.IsBatchMode)
        {
            AddPulls(node, ancestors, pulls);
        }

        ancestors.Add(node);

        foreach (var child in node.Children)
        {
            Visit(child, ancestors, pulls);
        }

        ancestors.RemoveAt(ancestors.Count - 1);
    }

    private static void AddPulls(PlanNode computeScalar, List<PlanNode> ancestors, List<ExpressionPull> pulls)
    {
        var sites = new Dictionary<(PlanNode Puller, ExpressionPullSite Site),
                                   (List<DefinedValueInfo> All, List<DefinedValueInfo> Output)>();

        foreach (var value in computeScalar.DefinedValues)
        {
            foreach (var (puller, site, isOutput) in GetPullers(computeScalar, value, ancestors).Distinct())
            {
                if (!sites.TryGetValue((puller, site), out var expressions))
                {
                    expressions = ([], []);

                    sites[(puller, site)] = expressions;
                }

                expressions.All.Add(value);

                if (isOutput)
                {
                    expressions.Output.Add(value);
                }
            }
        }

        pulls.AddRange(sites.Select(s => new ExpressionPull(computeScalar, s.Key.Puller, s.Key.Site, s.Value.All, s.Value.Output)));
    }

    private static IEnumerable<(PlanNode Puller, ExpressionPullSite Site, bool IsOutput)> GetPullers(PlanNode computeScalar,
                                                                                                      DefinedValueInfo value,
                                                                                                      List<PlanNode> ancestors)
    {
        HashSet<string> names = [.. value.Columns.Select(c => c.Column)];

        if (IsConstant(value))
        {
            yield return (ancestors[0], ExpressionPullSite.Startup, ancestors[0].Children.Any(c => IsCarried(c, names)));

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
                    yield return (parent, ExpressionPullSite.FirstInput, true);
                }

                yield break;
            }

            if (OperatorClassifier.IsComputeScalar(parent))
            {
                var derived = parent.DefinedValues.Where(d => d.References.Any(names.Contains)).ToList();

                names.UnionWith(derived.SelectMany(d => d.Columns.Select(c => c.Column)));

                child = parent;

                continue;
            }

            var input = parent.Children.IndexOf(child);

            if (OperatorClassifier.IsNestedLoop(parent) && input == 0 && parent.OuterReferences.Overlaps(names))
            {
                foreach (var inner in GetDescendants(parent.Children[1]).Where(n => n.ReferencedValues.Overlaps(names)))
                {
                    yield return (inner, ExpressionPullSite.FirstInput, false);
                }
            }

            var isCarried = IsCarried(parent, names);

            var isStored = isCarried && IsStored(parent, input);

            if (isStored || parent.ReferencedValues.Overlaps(names))
            {
                yield return (parent, input == 1 ? ExpressionPullSite.SecondInput : ExpressionPullSite.FirstInput, isStored);
            }

            if (isStored || !isCarried)
            {
                yield break;
            }

            child = parent;
        }
    }

    private static bool IsConstant(DefinedValueInfo value)
        => value.References.Count > 0 && value.References.All(r => r.StartsWith(ConstantPrefix, StringComparison.Ordinal));

    private static bool IsCarried(PlanNode node, HashSet<string> names) => node.OutputColumns.Any(c => names.Contains(c.Column));

    private static bool IsStored(PlanNode node, int input)
        => OperatorClassifier.IsSort(node) || OperatorClassifier.IsSpool(node) || (OperatorClassifier.IsHash(node) && input == 0);

    private static IEnumerable<PlanNode> GetDescendants(PlanNode node) => node.Children.SelectMany(GetDescendants).Prepend(node);
}
