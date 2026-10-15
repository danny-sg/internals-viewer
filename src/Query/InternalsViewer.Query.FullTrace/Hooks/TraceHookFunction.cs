namespace InternalsViewer.Query.FullTrace.Hooks;

internal readonly record struct TraceHookFunction(ulong Address, TraceHookEvents Events);
