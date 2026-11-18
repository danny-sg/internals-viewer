using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Execution.AccessPaths.Aggregation;

public sealed record AggregateColumn(string Column, AggregateFunction Function)
{
    public AccessExpression? Argument { get; init; }

    public bool IsDistinct { get; init; }

    public string ArgumentName => Argument is AccessExpression.Column column ? column.Name : string.Empty;

    public string ToText()
    {
        if (Function == AggregateFunction.CountStar)
        {
            return "COUNT(*)";
        }

        var distinct = IsDistinct ? "DISTINCT " : string.Empty;

        var argument = ArgumentName.Length > 0 ? ArgumentName : "*";

        return $"{Function.ToDisplayName()}({distinct}{argument})";
    }
}
