using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;

namespace InternalsViewer.Execution.AccessPaths.Aggregation;

public static class AggregateGroupValues
{
    public const string Expression = "Group By";

    public static IReadOnlyList<AggregateValue> Of(IReadOnlyList<string> groupBy, AccessKey key)
    {
        if (groupBy.Count == 0)
        {
            return [];
        }

        var values = new AggregateValue[groupBy.Count];

        for (var index = 0; index < groupBy.Count; index++)
        {
            var value = index < key.Count ? AccessValueFormatter.ToText(key[index]) : "NULL";

            values[index] = new AggregateValue(groupBy[index], Expression, value);
        }

        return values;
    }
}
