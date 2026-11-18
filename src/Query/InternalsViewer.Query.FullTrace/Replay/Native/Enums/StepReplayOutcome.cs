namespace InternalsViewer.Query.FullTrace.Replay.Native.Enums;

internal enum StepReplayOutcome : uint
{
    ReachedEnd = 0,
    FrameReturned = 1,
    StepLimitReached = 2
}
