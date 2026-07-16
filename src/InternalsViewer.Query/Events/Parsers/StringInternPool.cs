namespace InternalsViewer.Query.Events.Parsers;

/// <summary>
/// Interns spans drawn from a small fixed vocabulary into shared string instances
/// </summary>
internal sealed class StringInternPool
{
    private readonly Dictionary<string, string> _pool = new(StringComparer.Ordinal);

    public string Intern(ReadOnlySpan<char> value)
    {
        var lookup = _pool.GetAlternateLookup<ReadOnlySpan<char>>();

        if (lookup.TryGetValue(value, out var existing))
        {
            return existing;
        }

        var interned = value.ToString();

        _pool[interned] = interned;

        return interned;
    }
}
