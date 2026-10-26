namespace InternalsViewer.Query.FullTrace.Native.Enums;

internal enum HookEventType : uint
{
    Call = 0,
    Entry = 1,
    Return = 2,
    Abandoned = 3,
    FrameEnded = 4,
    StackUnwound = 5
}
