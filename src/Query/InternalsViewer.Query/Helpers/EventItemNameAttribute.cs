namespace InternalsViewer.Query.Helpers;

[AttributeUsage(AttributeTargets.Class |
                AttributeTargets.Struct |
                AttributeTargets.Enum |
                AttributeTargets.Field)]
public sealed class EventItemNameAttribute(string name) : Attribute
{
    public string Name { get; set; } = name;

    public string? Description { get; set; }

    public override string ToString() => Name;
}