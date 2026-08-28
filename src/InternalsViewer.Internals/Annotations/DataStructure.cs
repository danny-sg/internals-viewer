using InternalsViewer.Internals.Interfaces.Annotations;

namespace InternalsViewer.Internals.Annotations;

public class DataStructure : IDataStructure
{
    public List<DataStructureItem> MarkItems => field ??= [];

    public bool IsMarkEnabled { get; set; }

    /// <summary>
    /// Adds a marker that the given property is a part of the data structure at the offset
    /// </summary>
    public void MarkProperty(string propertyName, int offset, int length, string[]? tags = null)
    {
        MarkProperty(ItemType.None, propertyName, offset, length, tags);
    }

    public void MarkProperty(ItemType itemType, string propertyName, int offset, int length, string[]? tags = null, bool isVisible = true)
    {
        if (!IsMarkEnabled)
        {
            return;
        }

        var dataStructureItem = new PropertyItem
        {
            ItemType = itemType,
            PropertyName = propertyName,
            Offset = offset,
            Length = length,
            Tags = tags ?? [],
            IsVisible = isVisible
        };

        MarkItems.Add(dataStructureItem);
    }

    public void MarkArray(string propertyName, int startPosition, int length, int index)
    {
        if (!IsMarkEnabled)
        {
            return;
        }

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
        if (!IsMarkEnabled)
        {
            return;
        }

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
        if (!IsMarkEnabled)
        {
            return;
        }

        var dataStructureItem = new PropertyItem
        {
            PropertyName = propertyName
        };
        MarkItems.Add(dataStructureItem);
    }

    /// <summary>
    /// Adds a marker for a value that is not byte aligned, covering the bytes the bits fall within
    /// </summary>
    public void MarkBits(ItemType type, string name, object value, int bitOffset, int bitLength, string[]? tags = null)
    {
        if (!IsMarkEnabled)
        {
            return;
        }

        var dataStructureItem = new ValueItem
        {
            ItemType = type,
            Name = name,
            Value = value,
            Offset = bitOffset >> 3,
            Length = ((bitOffset & 7) + bitLength + 7) >> 3,
            BitOffset = bitOffset,
            BitLength = bitLength,
            Tags = tags ?? []
        };

        MarkItems.Add(dataStructureItem);
    }

    public void MarkValue(ItemType type, string name, object value, int offset, int length, string[]? tags = null)
    {
        if (!IsMarkEnabled)
        {
            return;
        }

        var dataStructureItem = new ValueItem
        {
            ItemType = type,
            Name = name,
            Value = value,
            Offset = offset,
            Length = length,
            Tags = tags ?? []
        };

        MarkItems.Add(dataStructureItem);
    }
}