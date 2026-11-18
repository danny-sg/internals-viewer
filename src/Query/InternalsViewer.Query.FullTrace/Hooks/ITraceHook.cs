namespace InternalsViewer.Query.FullTrace.Hooks;

/// <summary>
/// Trace Hook definition interface
/// </summary>
/// <remarks>
/// The TTD trace is replayed via the Time Travel Bridge, a C++ library that uses the TTD API. Interop between the .DLL and C# code is
/// expensive so the basic replay is done inside the bridge.
///
/// Hooks allow the managed code to jump in when certain functions (addresses) are found in the trace so memory can be read and decoded at
/// that point in the playback.
///
/// Control then passes back to the bridge to continue the replay.
/// </remarks>
internal interface ITraceHook
{
    IEnumerable<TraceHookFunction> Functions { get; }

    /// <summary>
    /// When a hooked function call is found, tracked and has been added as a CallNode
    /// </summary>
    void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
    }

    /// <summary>
    /// When a tracked call finishes and returns a value
    /// </summary>
    void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
    }

    /// <summary>
    /// When a tracked call finishes without returning a value
    /// </summary>
    void OnAbandoned(in TraceHookCall call)
    {
    }

    /// <summary>
    /// When a specific watched frame has ended
    /// </summary>
    void OnFrameEnded(in TraceHookCall call)
    {
    }
}
