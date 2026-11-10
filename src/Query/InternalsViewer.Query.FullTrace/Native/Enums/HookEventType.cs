namespace InternalsViewer.Query.FullTrace.Native.Enums;

internal enum HookEventType : uint
{
    Entry = 1,
    Return = 2,
    Abandoned = 3,
    FrameEnded = 4
}
