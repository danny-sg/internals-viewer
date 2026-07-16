namespace InternalsViewer.Query.Events.Operators;

public sealed record QueryThreadEvent : EngineEvent
{
    public int NodeId { get; set; }

    public override bool IsVisible => false;
}