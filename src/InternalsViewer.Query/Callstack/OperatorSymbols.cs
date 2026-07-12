namespace InternalsViewer.Query.Callstack;

/// <summary>
/// A frame's plan-operator classification: the operator it implements and the engine role it plays
/// </summary>
/// <remarks>
/// Rendered as a two-tone badge — <c>Name | Kind</c> (e.g. <c>Table Scan | Iterator</c>) — so the plan operator and
/// the kind of engine code read at a glance.
/// </remarks>
public sealed record OperatorTag(string Name, string Kind);

/// <summary>
/// Maps a query-engine class or method to the plan operator it implements
/// </summary>
/// <remarks>
/// The query execution engine runs each plan operator as a <c>CQScan*</c> Volcano iterator, so a callstack frame in
/// one of these classes is that operator's code. Matched by prefix so the <c>New</c>/variant suffixes are covered;
/// entries are ordered most-specific first (the first matching prefix wins). Seed list — expand from real dumps
/// (see the CQ-symbol enumeration test) and amend the <see cref="OperatorTag"/> parts as needed.
/// </remarks>
public static class OperatorSymbols
{
    private const string Iterator = "Iterator";

    private static readonly (string Prefix, OperatorTag Tag)[] Map =
    [
        ("CQScanHashAgg", new("Hash Match (Aggregate)", Iterator)),
        ("CQScanHash", new("Hash Match", Iterator)),
        ("CQScanMergeJoin", new("Merge Join", Iterator)),
        ("CQScanNLJoin", new("Nested Loops", Iterator)),
        ("CQScanStreamAgg", new("Stream Aggregate", Iterator)),
        ("CQScanTopSort", new("Sort", Iterator)),
        ("CQScanSort", new("Sort", Iterator)),
        ("CQScanTop", new("Top", Iterator)),
        ("CQScanRange", new("Index Seek", Iterator)),
        ("CQScanIndex", new("Index Scan", Iterator)),
        ("CQScanTable", new("Table Scan", Iterator)),
        ("CQScanRowset", new("Scan", Iterator)),
        ("CQScanFilter", new("Filter", Iterator)),
        ("CQScanComputeScalar", new("Compute Scalar", Iterator)),
        ("CQScanConcat", new("Concatenation", Iterator)),
        ("CQScanSpool", new("Spool", Iterator)),
        ("CQScanExchange", new("Parallelism", Iterator)),
        ("CQScanSeq", new("Sequence", Iterator)),
        ("CQScanUpdate", new("Update", Iterator)),
    ];

    /// <summary>
    /// The plan operator a class implements, a generic iterator tag for an unmapped CQScan, or null if not an iterator
    /// </summary>
    public static OperatorTag? Classify(string? className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return null;
        }

        foreach (var (prefix, tag) in Map)
        {
            if (className.StartsWith(prefix, StringComparison.Ordinal))
            {
                return tag;
            }
        }

        return className.StartsWith("CQScan", StringComparison.Ordinal) ? new OperatorTag("Operator", Iterator) : null;
    }
}
