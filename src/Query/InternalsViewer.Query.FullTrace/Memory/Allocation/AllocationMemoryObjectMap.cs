namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

internal sealed class AllocationMemoryObjectMap(Dictionary<ulong, string?[]> types)
{
    private Dictionary<ulong, string?[]> Types { get; } = types;

    public string? GetMemoryObject(ulong function, int callIndex)
        => Types.TryGetValue(function, out var calls) && callIndex >= 0 && callIndex < calls.Length ? calls[callIndex] : null;
}
