namespace InternalsViewer.Query.FullTrace.Native;

internal enum HookEventType : uint
{
    Entry = 0,
    Return = 1,
    Abandoned = 2
}
