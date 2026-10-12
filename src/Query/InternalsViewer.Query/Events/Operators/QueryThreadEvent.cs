using InternalsViewer.Query.Events.Properties;

namespace InternalsViewer.Query.Events.Operators;

internal sealed partial record QueryThreadEvent : EngineEvent
{
    [EventProperty("Node")]
    public int NodeId { get; set; }

    public override bool IsVisible => false;
}