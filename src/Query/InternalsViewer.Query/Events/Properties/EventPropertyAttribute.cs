namespace InternalsViewer.Query.Events.Properties;

[AttributeUsage(AttributeTargets.Property)]
public sealed class EventPropertyAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    public EventPropertyType Type { get; set; } = EventPropertyType.Default;

    public string? Format { get; set; }
}
