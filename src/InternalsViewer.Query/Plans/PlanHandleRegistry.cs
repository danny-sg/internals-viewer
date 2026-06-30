namespace InternalsViewer.Query.Plans;

/// <summary>
/// Interns plan handle strings to compact <see cref="short"/> ids. A plan handle is a long, opaque
/// binary string used only to link engine events back to the execution plan that produced them; it is
/// never displayed. Storing the string on every event wastes memory when a capture holds many thousands
/// of events, so each distinct handle is mapped once to a small id that events and plans reference
/// instead.
/// </summary>
public sealed class PlanHandleRegistry
{
    /// <summary>The id used for an event with no plan handle (the default for <see cref="short"/>).</summary>
    public const short None = 0;

    private readonly Dictionary<string, short> _idsByHandle = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the id for <paramref name="planHandle"/>, assigning a new one the first time a handle is
    /// seen. A null or empty handle maps to <see cref="None"/>.
    /// </summary>
    public short GetOrAdd(string? planHandle)
    {
        if (string.IsNullOrEmpty(planHandle))
        {
            return None;
        }

        if (_idsByHandle.TryGetValue(planHandle, out var id))
        {
            return id;
        }

        id = (short)(_idsByHandle.Count + 1);
        _idsByHandle[planHandle] = id;
        return id;
    }

    /// <summary>
    /// As <see cref="GetOrAdd(string?)"/>, but matches the handle text without allocating a string unless it
    /// is seen for the first time (so the same handle, repeated on every event, costs nothing after the first).
    /// </summary>
    public short GetOrAdd(ReadOnlySpan<char> planHandle)
    {
        if (planHandle.IsEmpty)
        {
            return None;
        }

        var lookup = _idsByHandle.GetAlternateLookup<ReadOnlySpan<char>>();

        if (lookup.TryGetValue(planHandle, out var id))
        {
            return id;
        }

        id = (short)(_idsByHandle.Count + 1);
        _idsByHandle[planHandle.ToString()] = id;
        return id;
    }
}
