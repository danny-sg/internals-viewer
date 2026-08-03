namespace InternalsViewer.Execution.AccessPaths.Joins;

/// <summary>
/// One slot of a hash table, holding every build row whose hash selected it
/// </summary>
public sealed class HashBucket(int index)
{
    public int Index { get; } = index;

    public IReadOnlyList<HashEntry> Entries => Chain;

    public int Count => Chain.Count;

    private List<HashEntry> Chain { get; } = [];

    internal int Add(HashEntry entry)
    {
        Chain.Add(entry);

        return Chain.Count - 1;
    }

    internal void MarkMatched(int entryIndex)
    {
        Chain[entryIndex] = Chain[entryIndex] with { IsMatched = true };
    }

    internal void Clear()
    {
        Chain.Clear();
    }
}
