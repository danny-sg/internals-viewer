namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;

public enum TraceInstructionOutcome
{
    ReachedEnd = 0,
    FrameReturned = 1,
    StepLimitReached = 2
}
