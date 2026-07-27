namespace InternalsViewer.Internals.DataAccess.AccessPaths.Search;

/// <summary>
/// The key range a seek is restricted to
/// </summary>
/// <remarks>
/// An unbounded start or end key represents an open ended range, which is how a seek with only
/// one boundary specified in the execution plan behaves.
/// </remarks>
public sealed record SeekBounds
{
    /// <summary>
    /// Range covering the whole index
    /// </summary>
    public static readonly SeekBounds All = new();

    public AccessKey StartKey { get; init; } = AccessKey.Unbounded;

    public bool StartInclusive { get; init; } = true;

    public AccessKey EndKey { get; init; } = AccessKey.Unbounded;

    public bool EndInclusive { get; init; } = true;

    /// <summary>
    /// Number of leading key columns taking part in comparisons
    /// </summary>
    public int CompareWidth { get; init; } = int.MaxValue;

    public bool HasStart => !StartKey.IsUnbounded;

    public bool HasEnd => !EndKey.IsUnbounded;

    /// <summary>
    /// Creates a range matching a single key value
    /// </summary>
    public static SeekBounds Equality(AccessKey key)
    {
        return new SeekBounds
        {
            StartKey = key,
            StartInclusive = true,
            EndKey = key,
            EndInclusive = true,
            CompareWidth = key.Count
        };
    }

    /// <summary>
    /// Creates a range between two keys
    /// </summary>
    public static SeekBounds Between(AccessKey startKey,
                                     AccessKey endKey,
                                     bool startInclusive = true,
                                     bool endInclusive = true)
    {
        return new SeekBounds
        {
            StartKey = startKey,
            StartInclusive = startInclusive,
            EndKey = endKey,
            EndInclusive = endInclusive,
            CompareWidth = Math.Max(startKey.Count, endKey.Count)
        };
    }
}
