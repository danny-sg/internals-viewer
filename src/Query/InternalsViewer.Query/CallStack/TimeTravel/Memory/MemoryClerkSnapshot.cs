namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public sealed record MemoryClerkSnapshot(IReadOnlyDictionary<ulong, string> Clerks, IReadOnlyDictionary<ulong, string> Objects)
{
    public static MemoryClerkSnapshot Empty { get; } = new(new Dictionary<ulong, string>(), new Dictionary<ulong, string>());

    public MemoryClerkSnapshot Merge(MemoryClerkSnapshot later) => new(Merged(Clerks, later.Clerks), Merged(Objects, later.Objects));

    private static Dictionary<ulong, string> Merged(IReadOnlyDictionary<ulong, string> earlier, IReadOnlyDictionary<ulong, string> later)
    {
        var merged = new Dictionary<ulong, string>(earlier);

        foreach (var (address, type) in later)
        {
            merged[address] = type;
        }

        return merged;
    }
}
