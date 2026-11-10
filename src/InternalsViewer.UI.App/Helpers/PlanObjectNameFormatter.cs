using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Helpers;

public static class PlanObjectNameFormatter
{
    public static string Format(PlanNode node)
    {
        if (string.IsNullOrEmpty(node.Table))
        {
            return string.Empty;
        }

        var name = string.IsNullOrEmpty(node.Schema) ? node.Table : $"{node.Schema}.{node.Table}";

        return string.IsNullOrEmpty(node.Index) ? name : $"{node.Index}\n{name}";
    }
}
