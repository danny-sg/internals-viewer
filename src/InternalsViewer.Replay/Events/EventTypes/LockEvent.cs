using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Locks;

namespace InternalsViewer.Replay.Events.EventTypes;

public sealed record LockEvent : EngineEvent
{
    public LockMode LockMode { get; init; }

    public LockResourceType ResourceType { get; init; }

    public RowIdentifier? RowIdentifier { get; set; }

    public string? KeyHash { get; set; }

    public override string Description => $"Lock: {LockMode}/{ResourceType}";
}