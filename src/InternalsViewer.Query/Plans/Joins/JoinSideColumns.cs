using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Joins;

/// <summary>
/// Relates a join's key columns to the side that supplies them
/// </summary>
/// <remarks>
/// A side that reads a table names it outright, but a side that is itself an operator names nothing, so the tables have to be gathered
/// from the whole subtree. Checking the subtree rather than the node is what lets a join read another join.
/// </remarks>
internal static class JoinSideColumns
{
    /// <summary>
    /// Whether every key names a table the side reads somewhere beneath it
    /// </summary>
    public static bool KeysMatchSide(List<ColumnReference> keys, PlanNode side)
    {
        var tables = Tables(side);

        return keys.All(k => k.Table.Length == 0 || tables.Contains(Trim(k.Table)));
    }

    private static HashSet<string> Tables(PlanNode side)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Collect(side, tables);

        return tables;
    }

    private static void Collect(PlanNode node, HashSet<string> tables)
    {
        if (!string.IsNullOrEmpty(node.Table))
        {
            tables.Add(Trim(node.Table));
        }

        foreach (var child in node.Children)
        {
            Collect(child, tables);
        }
    }

    private static string Trim(string name) => name.Trim('[', ']');
}
