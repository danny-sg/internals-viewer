using InternalsViewer.Query.FullTrace.Hooks;

namespace InternalsViewer.Query.FullTrace.Interfaces.Hooks;

internal interface ITraceReplay
{
    int Read(ulong address, Span<byte> destination);

    bool WatchFrame(uint thread, ulong frame);

    void WatchStack(uint thread, ulong stackPointer);

    ReadOnlySpan<TraceHookFrame> GetFrames(uint thread);
}
