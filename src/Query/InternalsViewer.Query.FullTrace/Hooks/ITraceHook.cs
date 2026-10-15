namespace InternalsViewer.Query.FullTrace.Hooks;

internal interface ITraceHook
{
    IEnumerable<ulong> Functions { get; }

    void OnEntry(in TraceHookCall call, ITraceMemory memory);

    void OnReturn(in TraceHookCall call, ITraceMemory memory);

    void OnAbandoned(in TraceHookCall call);
}
