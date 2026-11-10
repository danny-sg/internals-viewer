using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Query.Plans.Joins;

public sealed record CorrelatedSeekColumn(string Column,
                                          string OuterTable,
                                          string OuterColumn,
                                          ComparisonOperator Comparison = ComparisonOperator.Equal)
{
    public string OuterReference => OuterTable.Length == 0 ? OuterColumn : $"{OuterTable}.{OuterColumn}";

    public bool IsEquality => Comparison == ComparisonOperator.Equal;
}
