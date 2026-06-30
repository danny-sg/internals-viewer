namespace InternalsViewer.Internals.Annotations;

/// <summary>
/// Custom attribute to store mark properties
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DataStructureItemAttribute(ItemType itemType, string description)
    : Attribute
{
    public DataStructureItemAttribute(ItemType itemType)
        : this(itemType, string.Empty)
    {
    }

    public string Name { get; set; } = description;

    public ItemType ItemType { get; set; } = itemType;

    public bool IsVisible { get; set; } = true;
}