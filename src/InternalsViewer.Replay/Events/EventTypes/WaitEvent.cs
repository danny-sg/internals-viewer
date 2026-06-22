using InternalsViewer.Replay.Locks;

namespace InternalsViewer.Replay.Events.EventTypes;

public sealed record WaitEvent : EngineEvent
{
    public WaitType WaitType { get; set; }

    public override string Description => $"Wait: {WaitType}";
}