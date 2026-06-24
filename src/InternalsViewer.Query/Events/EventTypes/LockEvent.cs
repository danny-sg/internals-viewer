using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Locks;

namespace InternalsViewer.Query.Events.EventTypes;

public sealed record LockEvent : EngineEvent
{
    public LockMode LockMode { get; init; }

    public LockResourceType ResourceType { get; init; }

    public RowIdentifier? RowIdentifier { get; set; }

    public string? KeyHash { get; set; }

    public override string Description => $"Lock: {LockMode}/{ResourceType}";
}