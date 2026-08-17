using System.Data;

namespace InternalsViewer.Execution.AccessPaths.Windowing;

public static class RankingFunctions
{
    /// <summary>
    /// The type every ranking function produces
    /// </summary>
    /// <remarks>
    /// All three count rows, which the engine does in eight bytes, so the plan puts a Compute Scalar above the Sequence Project to convert
    /// down to the int the query returns.
    /// </remarks>
    public const SqlDbType ResultType = SqlDbType.BigInt;

    public static RankingFunction? Parse(string? name)
        => name?.Trim().ToUpperInvariant() switch
        {
            "ROW_NUMBER" or "ROWNUMBER" => RankingFunction.RowNumber,
            "RANK" => RankingFunction.Rank,
            "DENSE_RANK" or "DENSERANK" => RankingFunction.DenseRank,
            _ => null
        };

    public static string ToDisplayName(this RankingFunction function)
        => function switch
        {
            RankingFunction.RowNumber => "ROW_NUMBER",
            RankingFunction.Rank => "RANK",
            RankingFunction.DenseRank => "DENSE_RANK",
            _ => function.ToString()
        };
}
