using InternalsViewer.Replay.Locks;

namespace InternalsViewer.Replay.Events;

public sealed record WaitEvent : EngineEvent
{
    public WaitType WaitType { get; set; }

    public override string Description => $"{WaitType}";
}