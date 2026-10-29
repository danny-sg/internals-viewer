using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Joins;

public static class BitmapProbeResolver
{
    public static List<BitmapProbe> Resolve(ExecutionPlan plan)
    {
        var probes = new List<BitmapProbe>();

        foreach (var node in plan.NodesById.Values)
        {
            foreach (var bitmap in node.ProbedBitmaps)
            {
                if (GetCreator(plan, bitmap) is { } creator && !ReferenceEquals(creator, node))
                {
                    probes.Add(new BitmapProbe(creator, node, bitmap));
                }
            }
        }

        return probes;
    }

    private static PlanNode? GetCreator(ExecutionPlan plan, string bitmap)
    {
        foreach (var node in plan.NodesById.Values)
        {
            foreach (var definedValue in node.DefinedValues)
            {
                foreach (var column in definedValue.Columns)
                {
                    if (string.IsNullOrEmpty(column.Table) && string.Equals(column.Column, bitmap, StringComparison.OrdinalIgnoreCase))
                    {
                        return node;
                    }
                }
            }
        }

        return null;
    }
}
