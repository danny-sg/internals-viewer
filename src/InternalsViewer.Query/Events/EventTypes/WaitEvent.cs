using InternalsViewer.Query.Locks;

namespace InternalsViewer.Query.Events.EventTypes;

public sealed record WaitEvent : EngineEvent
{
    public WaitType WaitType { get; set; }

    public override string Description => $"Wait: {WaitType}";
}