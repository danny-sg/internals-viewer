namespace InternalsViewer.Query.FullTrace.Memory;

internal sealed class AllocationMemoryObjects(Dictionary<ulong, string?[]> types)
{
    public static AllocationMemoryObjects Empty { get; } = new([]);

    private Dictionary<ulong, string?[]> Types { get; } = types;

    public string? GetMemoryObject(ulong function, int callIndex)
        => Types.TryGetValue(function, out var calls) && callIndex >= 0 && callIndex < calls.Length ? calls[callIndex] : null;
}
