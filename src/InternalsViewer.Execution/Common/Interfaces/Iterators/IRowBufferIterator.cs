using InternalsViewer.Execution.Common.AccessPaths.Joins;

namespace InternalsViewer.Execution.Common.Interfaces.Iterators;

public interface IRowBufferIterator : IIterator
{
    IReadOnlyList<RowBuffer> Buffers { get; }
}
