using System.Collections.Concurrent;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

internal sealed class BlockCache<T>(Func<int, T> find)
{
    private ConcurrentDictionary<int, T> Blocks { get; } = new();

    public T Get(int block) => Blocks.GetOrAdd(block, find);
}
