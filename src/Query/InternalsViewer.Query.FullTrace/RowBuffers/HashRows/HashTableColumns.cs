using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.RowBuffers.HashRows;

public static class HashTableColumns
{
    public static IReadOnlyList<ColumnReference> Get(PlanNode hashMatch)
        => [.. hashMatch.Children.FirstOrDefault()?.OutputColumns ?? [], .. hashMatch.HashInfo?.BuildKeys ?? []];

    public static int GetKeyCount(PlanNode hashMatch) => hashMatch.HashInfo?.BuildKeys.Count ?? 0;
}
