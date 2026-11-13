using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Memory;

public interface ITraceMemoryReader : IMemoryReader
{
    EngineLayout Layout { get; }
}
