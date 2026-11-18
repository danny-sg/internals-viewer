using InternalsViewer.Query.FullTrace.Replay.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Replay.Hooks;

/// <summary>
/// Hook function definition defining address and events to trigger a callback
/// </summary>
internal readonly record struct TraceHookFunction(ulong Address, TraceHookEvents Events);
