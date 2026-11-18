namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public sealed record TraceHashBucket(int Index, ulong Address, List<TraceHashItem> Items)
{
    public List<TraceHashEntry> Entries => field ??= [.. Items.SelectMany(i => i.Entries)];

    public int Count => Entries.Count;
}
