using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Search;

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
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

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

    public override string ToString()
    {
        return IsUnbounded ? "*" : string.Join(", ", Values);
    }
}
