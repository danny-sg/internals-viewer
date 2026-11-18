namespace InternalsViewer.Query.FullTrace.Replay.Hooks.Enums;

[Flags]
internal enum TraceHookEvents : uint
{
    None = 0,
    Entry = 2,
    Return = 4
}
