using InternalsViewer.Execution.AccessPaths.Memory;

namespace InternalsViewer.Execution.Interfaces.Iterators;

/// <summary>
/// An operator that holds rows in memory, and can say how much of it that takes
/// </summary>
/// <remarks>
/// What it is holding is also the most it has held, because a buffer keeps its rows until the operator closes: a sort holds its run while
/// it emits from it, and a hash table holds its build side through the probe.
/// </remarks>
public interface IMemoryBufferIterator : IIterator
{
    BufferMemory Memory { get; }
}
