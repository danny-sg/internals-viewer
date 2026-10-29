namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

internal sealed record BufferPoolFunctions(ulong[] Gets, ulong[] PostReads)
{
    public static BufferPoolFunctions Empty { get; } = new([], []);

    public static BufferPoolFunctions Merge(IEnumerable<BufferPoolFunctions> functions)
    {
        var all = functions.ToList();

        return new([.. all.SelectMany(f => f.Gets)], [.. all.SelectMany(f => f.PostReads)]);
    }

    public BufferPoolFunctions Rebase(ulong address) => new([.. Gets.Select(g => g + address)], [.. PostReads.Select(p => p + address)]);

    internal sealed class Collector
    {
        private const string GetFunction = "BPool::Get";

        private const string PostReadFunction = "FCB::PostRead";

        private List<ulong> Gets { get; } = [];

        private List<ulong> PostReads { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            if (name.SequenceEqual(GetFunction))
            {
                Gets.Add(rva);
            }
            else if (name.SequenceEqual(PostReadFunction))
            {
                PostReads.Add(rva);
            }
        }

        public BufferPoolFunctions Build() => new([.. Gets], [.. PostReads]);
    }
}
