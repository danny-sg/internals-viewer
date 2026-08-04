using InternalsViewer.Execution.AccessPaths.Joins;

namespace InternalsViewer.Execution.Interfaces.Iterators;

public interface IRowBufferIterator : IIterator
{
    IReadOnlyList<RowBuffer> Buffers { get; }
}
