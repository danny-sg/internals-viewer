using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Hooks;

/// <summary>
/// Hook function definition defining address and events to trigger a callback
/// </summary>
internal readonly record struct TraceHookFunction(ulong Address, TraceHookEvents Events);
