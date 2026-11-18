namespace InternalsViewer.Execution.AccessPaths.Search;

/// <summary>
/// Key range for a seek
/// </summary>
/// <remarks>
/// An unbounded start or end value represents an open-ended range, which is how a seek with only one boundary specified in the execution
/// plan behaves.
/// </remarks>
public sealed record SeekBounds
{
    /// <summary>
    /// Range covering the whole index
    /// </summary>
    public static readonly SeekBounds All = new();

    public AccessKey StartValue { get; init; } = AccessKey.Unbounded;

    public bool IsStartInclusive { get; init; } = true;

    public AccessKey EndValue { get; init; } = AccessKey.Unbounded;

    public bool IsEndInclusive { get; init; } = true;

    /// <summary>
    /// Number of leading key columns taking part in comparisons
    /// </summary>
    public int CompareWidth { get; init; } = int.MaxValue;

    public bool HasStart => !StartValue.IsUnbounded;

    public bool HasEnd => !EndValue.IsUnbounded;

    public SeekBounds Reversed()
    {
        return new SeekBounds
        {
            StartValue = EndValue,
            IsStartInclusive = IsEndInclusive,
            EndValue = StartValue,
            IsEndInclusive = IsStartInclusive,
            CompareWidth = CompareWidth
        };
    }

    /// <summary>
    /// Creates a range matching a single key value
    /// </summary>
    public static SeekBounds Equality(AccessKey value)
    {
        return new SeekBounds
        {
            StartValue = value,
            IsStartInclusive = true,
            EndValue = value,
            IsEndInclusive = true,
            CompareWidth = value.Count
        };
    }

    /// <summary>
    /// Creates a range between two key values
    /// </summary>
    public static SeekBounds Between(AccessKey startValue,
                                     AccessKey endValue,
                                     bool isStartInclusive = true,
                                     bool isEndInclusive = true)
    {
        return new SeekBounds
        {
            StartValue = startValue,
            IsStartInclusive = isStartInclusive,
            EndValue = endValue,
            IsEndInclusive = isEndInclusive,
            CompareWidth = Math.Max(startValue.Count, endValue.Count)
        };
    }
}
