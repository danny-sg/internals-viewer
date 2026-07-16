namespace InternalsViewer.Query.Events.Parsers;

/// <summary>
/// Interns spans drawn from a small fixed vocabulary into shared string instances
/// </summary>
/// <remarks>
/// Event/field names and call stack module, pdb and guid values each come from a handful of distinct strings that
/// recur across every one of hundreds of thousands of events. Turning each distinct value into a string once and
/// reusing that instance keeps the repeated occurrences off the heap, which is what removes the GC pressure of a load.
/// Scoped to a single parse session, so the pool is discarded with it rather than growing for the process lifetime.
/// </remarks>
public sealed class StringInternPool
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
