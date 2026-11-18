namespace InternalsViewer.Query.Plans.Search;

/// <summary>
/// Key range for a seek
/// </summary>
/// <remarks>
/// An unbounded start or end value represents an open-ended range, which is how a seek with only one boundary specified in the execution
/// plan behaves.
/// </remarks>
public sealed record SeekBounds
{
    public AccessKey StartValue { get; init; } = AccessKey.Unbounded;

    public bool IsStartInclusive { get; init; } = true;

    public AccessKey EndValue { get; init; } = AccessKey.Unbounded;

    public bool IsEndInclusive { get; init; } = true;

    public bool HasStart => !StartValue.IsUnbounded;

    public bool HasEnd => !EndValue.IsUnbounded;

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
            IsEndInclusive = true
        };
    }
}
