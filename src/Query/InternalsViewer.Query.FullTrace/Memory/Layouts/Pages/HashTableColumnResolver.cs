using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;

public static class HashTableColumnResolver
{
    public static List<ColumnReference> Get(PlanNode hashMatch)
        => [.. hashMatch.Children.FirstOrDefault()?.OutputColumns ?? [], .. hashMatch.HashInfo?.BuildKeys ?? []];

    public static int GetKeyCount(PlanNode hashMatch) => hashMatch.HashInfo?.BuildKeys.Count ?? 0;
}
