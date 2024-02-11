namespace InternalsViewer.Internals.Annotations;

/// <summary>
/// Custom attribute to store mark properties
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DataStructureItemAttribute(ItemType itemType, string description)
    : Attribute
{
    public string Name { get; set; } = description;

    public ItemType ItemType { get; set; } = itemType;

    public DataStructureItemAttribute(ItemType itemType)
        : this(itemType, string.Empty)
    {
    }

    public bool IsVisible { get; set; } = true;
}