using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Replay.Hooks.Interfaces;

internal interface ITraceReplay : ITraceMemoryReader
{
    bool WatchFrame(uint thread, ulong frame);

    ReadOnlySpan<TraceHookFrame> GetFrames(uint thread);

    ReadOnlySpan<ulong> GetStackArguments(in TraceHookCall call);
}
