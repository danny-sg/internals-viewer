namespace InternalsViewer.Query.FullTrace.Replay.Native.Enums;

internal enum HookEventType : uint
{
    Entry = 1,
    Return = 2,
    Abandoned = 3,
    FrameEnded = 4
}
