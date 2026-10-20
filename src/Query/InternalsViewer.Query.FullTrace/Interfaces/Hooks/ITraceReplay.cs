using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Interfaces.Hooks;

internal interface ITraceReplay : IMemoryReader
{
    bool WatchFrame(uint thread, ulong frame);

    void WatchStack(uint thread, ulong stackPointer);

    ReadOnlySpan<TraceHookFrame> GetFrames(uint thread);
}
