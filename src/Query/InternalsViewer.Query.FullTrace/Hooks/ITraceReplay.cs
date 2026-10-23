using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal interface ITraceReplay : IMemoryReader
{
    bool WatchFrame(uint thread, ulong frame);

    ReadOnlySpan<TraceHookFrame> GetFrames(uint thread);
}
