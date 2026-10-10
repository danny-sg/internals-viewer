using InternalsViewer.Query.Events.Properties;

namespace InternalsViewer.Query.Events.Memory;

public sealed partial record MemoryEvent : EngineEvent
{
    [EventProperty("Used Memory", Type = EventPropertyType.Kilobytes)]
    public long? UsedMemoryKb { get; set; }

    [EventProperty("Granted Memory", Type = EventPropertyType.Kilobytes)]
    public long? GrantedMemoryKb { get; set; }

    [EventProperty("Ideal Additional Memory Before", Type = EventPropertyType.Kilobytes)]
    public long? AdditionalMemoryBeforeKb { get; set; }

    [EventProperty("Ideal Additional Memory After", Type = EventPropertyType.Kilobytes)]
    public long? AdditionalMemoryAfterKb { get; set; }
}