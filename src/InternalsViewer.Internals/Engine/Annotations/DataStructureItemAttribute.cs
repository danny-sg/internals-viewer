namespace InternalsViewer.Internals.Engine.Annotations;

/// <summary>
/// Custom attribute to store mark properties
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DataStructureItemAttribute(DataStructureItemType dataStructureItemType, string description)
    : Attribute
{
    public string Description { get; set; } = description;

    public DataStructureItemType DataStructureItemType { get; set; } = dataStructureItemType;

    public DataStructureItemAttribute(DataStructureItemType dataStructureItemType)
        : this(dataStructureItemType, string.Empty)
    {
    }

    public bool IsVisible { get; set; }
}