namespace InternalsViewer.Query.Events.Scheduling;

public enum TaskTransition
{
    Resuming = 0,
    InstantResuming = 1,
    Suspending = 2,
    SwitchToPreemptive = 3,
    SwitchToNonPreemptive = 4,
    Attach = 5,
    Detach = 6
}
