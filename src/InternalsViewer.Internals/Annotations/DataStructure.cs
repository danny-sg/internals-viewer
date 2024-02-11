namespace InternalsViewer.Internals.Annotations;

public class DataStructure
{
    /// <summary>
    /// Adds a marker that the given property is a part of the data structure at the offset
    /// </summary>
    public void MarkProperty(string propertyName, int offset, int length, List<string>? tags = null)
    {
        var dataStructureItem = new PropertyItem
        {
            PropertyName = propertyName,
            Offset = offset,
            Length = length,
            Tags = tags ?? new()
        };

        MarkItems.Add(dataStructureItem);
    }

    public void MarkArray(string propertyName, int startPosition, int length, int index)
    {
        var dataStructureItem = new PropertyItem
        {
            PropertyName = propertyName,
            Offset = startPosition,
            Length = length,
            Index = index
        };

        MarkItems.Add(dataStructureItem);
    }

    public void MarkProperty(string propertyName, string prefix, int index)
    {
        var dataStructureItem = new PropertyItem
        {
            PropertyName = propertyName,
            Prefix = prefix,
            Index = index
        };

        MarkItems.Add(dataStructureItem);
    }

    public void MarkProperty(string propertyName)
    {
        var dataStructureItem = new PropertyItem
        {
            PropertyName = propertyName
        };
        MarkItems.Add(dataStructureItem);
    }

    public void MarkValue(ItemType type, string name, object value, int offset, int length, List<string>? tags = null)
    {
        var dataStructureItem = new ValueItem
        {
            ItemType = type,
            Name = name,
            Value = value,
            Offset = offset,
            Length = length,
            Tags = tags ?? new()
        };

        MarkItems.Add(dataStructureItem);
    }

    public List<DataStructureItem> MarkItems { get; } = new();
}