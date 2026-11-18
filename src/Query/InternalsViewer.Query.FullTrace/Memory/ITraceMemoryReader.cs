using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Memory;

public interface ITraceMemoryReader : IMemoryReader
{
    EngineLayout Layout { get; }
}
