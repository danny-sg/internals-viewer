namespace InternalsViewer.Query.Memory;

public sealed record MemoryClerkSnapshot(IReadOnlyDictionary<ulong, string> Clerks,
                                         IReadOnlyDictionary<ulong, string> ObjectClerks,
                                         IReadOnlyDictionary<ulong, MemoryObjectInfo> MemoryObjects)
{
    public static MemoryClerkSnapshot Empty { get; } = new(new Dictionary<ulong, string>(),
                                                           new Dictionary<ulong, string>(),
                                                           new Dictionary<ulong, MemoryObjectInfo>());

    public MemoryClerkSnapshot Merge(MemoryClerkSnapshot later)
        => new(Merge(Clerks, later.Clerks), Merge(ObjectClerks, later.ObjectClerks), Merge(MemoryObjects, later.MemoryObjects));

    private static Dictionary<ulong, T> Merge<T>(IReadOnlyDictionary<ulong, T> earlier, IReadOnlyDictionary<ulong, T> later)
    {
        var merged = new Dictionary<ulong, T>(earlier);

        foreach (var (address, value) in later)
        {
            merged[address] = value;
        }

        return merged;
    }
}
