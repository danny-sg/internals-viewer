using InternalsViewer.Query.FullTrace.Hooks;

namespace InternalsViewer.Query.FullTrace.Interfaces.Hooks;

internal interface ITraceHook
{
    IEnumerable<TraceHookFunction> Functions { get; }

    void OnCall(in TraceHookCall call, ITraceReplay replay)
    {
    }

    void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
    }

    void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
    }

    void OnAbandoned(in TraceHookCall call)
    {
    }

    void OnFrameEnded(in TraceHookCall call)
    {
    }

    void OnStackUnwound(in TraceHookCall call)
    {
    }
}
