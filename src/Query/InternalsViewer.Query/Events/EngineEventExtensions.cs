namespace InternalsViewer.Query.Events;

public static class EngineEventExtensions
{
    public static ulong TaskKey(this EngineEvent engineEvent)
        => engineEvent.TaskAddress ?? engineEvent.WorkerAddress ?? (ulong)engineEvent.ThreadId;
}
