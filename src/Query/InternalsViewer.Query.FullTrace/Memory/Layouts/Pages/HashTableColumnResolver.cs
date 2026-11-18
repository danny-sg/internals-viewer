using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;

public static class HashTableColumnResolver
{
    public static List<ColumnReference> Get(PlanNode hashMatch)
        => [.. hashMatch.Children.FirstOrDefault()?.OutputColumns ?? [], .. hashMatch.HashInfo?.BuildKeys ?? []];

    public static List<ColumnReference> GetProbe(PlanNode hashMatch)
        => [.. hashMatch.Children.ElementAtOrDefault(1)?.OutputColumns ?? [], .. hashMatch.HashInfo?.ProbeKeys ?? []];

    public static int GetKeyCount(PlanNode hashMatch) => hashMatch.HashInfo?.BuildKeys.Count ?? 0;
}
