using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.AccessPaths.Search;

/// <summary>
/// An ordered set of key column values
/// </summary>
public readonly record struct AccessKey(ImmutableArray<AccessValue> Values)
{
    public static readonly AccessKey Unbounded = new([]);

    public ImmutableArray<AccessValue> Values { get; } = Values;

    public int Count => Values.IsDefault ? 0 : Values.Length;

    public bool IsUnbounded => Count == 0;

    public AccessValue this[int index] => Values[index];

    public static AccessKey Create(params AccessValue[] values)
    {
        return new AccessKey([.. values]);
    }

    /// <summary>
    /// Compares this key against another, considering only the leading columns
    /// </summary>
    /// <remarks>
    /// A width shorter than the full key gives partial matching, which is how a seek on a composite index with only leading columns
    /// bounded behaves.
    /// </remarks>
    public int ComparePrefix(in AccessKey other, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);

        var length = Math.Min(width, Math.Min(Count, other.Count));

        for (var index = 0; index < length; index++)
        {
            var result = AccessValueComparer.Compare(Values[index], other.Values[index]);

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    public bool Equals(AccessKey other)
    {
        if (Count != other.Count)
        {
            return false;
        }

        for (var index = 0; index < Count; index++)
        {
            if (!Values[index].Equals(other.Values[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = default(HashCode);

        for (var index = 0; index < Count; index++)
        {
            hash.Add(Values[index]);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return IsUnbounded ? "*" : string.Join(", ", Values);
    }
}
