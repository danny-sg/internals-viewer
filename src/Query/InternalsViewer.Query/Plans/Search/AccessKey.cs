using System.Collections.Immutable;
using InternalsViewer.Query.Plans.Values;

namespace InternalsViewer.Query.Plans.Search;

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
