namespace InternalsViewer.Internals.DataAccess.AccessPaths.Results;

/// <summary>
/// Running cost and cardinality totals for an access path
/// </summary>
/// <remarks>
/// Immutable so a total captured against a step stays fixed at the value it had when that step was
/// produced. Executors thread a running value through the walk and publish each new total, letting a
/// caller track counts without sharing mutable state with the executor.
/// </remarks>
public readonly record struct AccessCounters
{
    /// <summary>
    /// Pages read, equivalent to logical reads
    /// </summary>
    public long PagesRead { get; init; }

    /// <summary>
    /// Key comparisons performed, including binary search probes and boundary checks
    /// </summary>
    public long Comparisons { get; init; }

    /// <summary>
    /// Rows examined, excluding ghosts and the row that ended the range
    /// </summary>
    public long RowsRead { get; init; }

    /// <summary>
    /// Rows satisfying every predicate
    /// </summary>
    public long RowsOutput { get; init; }

    /// <summary>
    /// Ghost records skipped without comparison
    /// </summary>
    public long GhostsSkipped { get; init; }

    /// <summary>
    /// Leaf level page links followed
    /// </summary>
    public long LeafLinksFollowed { get; init; }

    public AccessCounters AddPageRead()
    {
        return this with { PagesRead = PagesRead + 1 };
    }

    public AccessCounters AddComparisons(long count)
    {
        return this with { Comparisons = Comparisons + count };
    }

    public AccessCounters AddRowRead()
    {
        return this with { RowsRead = RowsRead + 1 };
    }

    public AccessCounters AddRowOutput()
    {
        return this with { RowsOutput = RowsOutput + 1 };
    }

    public AccessCounters AddGhostSkipped()
    {
        return this with { GhostsSkipped = GhostsSkipped + 1 };
    }

    public AccessCounters AddLeafLinkFollowed()
    {
        return this with { LeafLinksFollowed = LeafLinksFollowed + 1 };
    }
}
