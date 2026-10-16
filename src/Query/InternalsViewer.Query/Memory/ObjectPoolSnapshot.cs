namespace InternalsViewer.Query.Memory;

public sealed partial class ObjectPoolSnapshot(IReadOnlyList<ObjectPoolEntry> entries)
{
    public static ObjectPoolSnapshot Empty { get; } = new([]);

    public IReadOnlyList<ObjectPoolEntry> Entries { get; } = entries;
}
