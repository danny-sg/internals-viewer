namespace InternalsViewer.Query.FullTrace.Native.Enums;

internal enum StepReplayOutcome : uint
{
    ReachedEnd = 0,
    FrameReturned = 1,
    StepLimitReached = 2
}
