using System.Data;

namespace InternalsViewer.Execution.AccessPaths.Aggregation;

public static class AggregateFunctions
{
    public static AggregateFunction? Parse(string? name)
        => name?.Trim().ToUpperInvariant() switch
        {
            "COUNTSTAR" or "COUNT*" => AggregateFunction.CountStar,
            "COUNT" => AggregateFunction.Count,
            "COUNT_BIG" or "COUNTBIG" => AggregateFunction.CountBig,
            "MIN" => AggregateFunction.Min,
            "MAX" => AggregateFunction.Max,
            "SUM" => AggregateFunction.Sum,
            "AVG" => AggregateFunction.Average,
            "ANY" => AggregateFunction.Any,
            _ => null
        };

    public static SqlDbType? ResultType(AggregateFunction function, SqlDbType? argumentType)
        => function switch
        {
            AggregateFunction.CountStar or AggregateFunction.CountBig
                => SqlDbType.BigInt,
            AggregateFunction.Count
                => SqlDbType.Int,
            AggregateFunction.Average or AggregateFunction.Sum
                => argumentType switch
                {
                    SqlDbType.TinyInt or SqlDbType.SmallInt or SqlDbType.Int => SqlDbType.Int,
                    SqlDbType.Real => SqlDbType.Float,
                    _ => argumentType
                },
            _ => argumentType
        };

    public static string ToDisplayName(this AggregateFunction function)
        => function switch
        {
            AggregateFunction.CountStar => "COUNT(*)",
            AggregateFunction.Count => "COUNT",
            AggregateFunction.CountBig => "COUNT_BIG",
            AggregateFunction.Min => "MIN",
            AggregateFunction.Max => "MAX",
            AggregateFunction.Sum => "SUM",
            AggregateFunction.Average => "AVG",
            _ => "ANY"
        };
}
