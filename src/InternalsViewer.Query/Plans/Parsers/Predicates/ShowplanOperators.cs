using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Query.Parsing.Plans.Predicates;

/// <summary>
/// Translates showplan comparison and scan type values into access path equivalents
/// </summary>
internal static class ShowplanOperators
{
    /// <summary>
    /// Maps a CompareOp attribute onto a comparison operator
    /// </summary>
    /// <remarks>
    /// IS and IS NOT are equality and inequality that treat nulls as comparable. They are mapped to their ordinary counterparts because
    /// null handling belongs to the evaluator.
    /// </remarks>
    public static ComparisonOperator? ParseComparison(string? compareOp)
    {
        return compareOp switch
        {
            "EQ" or "IS" => ComparisonOperator.Equal,
            "NE" or "IS NOT" => ComparisonOperator.NotEqual,
            "LT" => ComparisonOperator.LessThan,
            "LE" => ComparisonOperator.LessThanOrEqual,
            "GT" => ComparisonOperator.GreaterThan,
            "GE" => ComparisonOperator.GreaterThanOrEqual,
            _ => null
        };
    }

    /// <summary>
    /// Whether a seek range boundary described by a ScanType includes the boundary value
    /// </summary>
    public static bool IsInclusiveBoundary(string? scanType)
    {
        return scanType is "LE" or "GE" or "EQ" or "IS";
    }

    /// <summary>
    /// Whether a ScanType describes the start of a range rather than the end
    /// </summary>
    public static bool IsStartBoundary(string? scanType)
    {
        return scanType is "GT" or "GE";
    }
}
