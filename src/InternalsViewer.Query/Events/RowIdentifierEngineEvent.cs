using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events;

/// <summary>
/// An engine event linked to a single page/slot
/// </summary>
public abstract partial record RowIdentifierEngineEvent : PageEngineEvent
{
    [EventProperty("Row", Type = EventPropertyType.RowIdentifier)]
    public virtual RowIdentifier? RowIdentifier { get; set; }
}