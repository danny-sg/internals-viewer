namespace InternalsViewer.Query.FullTrace.Hooks;

[Flags]
internal enum TraceHookEvents : uint
{
    None = 0,
    Call = 1,
    Entry = 2,
    Return = 4
}
