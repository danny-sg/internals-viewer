using System.Collections.Generic;

namespace InternalsViewer.Internals;

public class DataStructure
{
    public void MarkDataStructure(string propertyName, int startPosition, int length)
    {
        MarkItems.Add(new MarkItem(propertyName, startPosition, length));
    }

    public void MarkDataStructure(string propertyName, int startPosition, int length, int index)
    {
        MarkItems.Add(new MarkItem(propertyName, startPosition, length, index));
    }

    public void MarkDataStructure(string propertyName, string prefix, int startPosition, int length, int index)
    {
        MarkItems.Add(new MarkItem(propertyName, startPosition, length, index));
    }

    public void MarkDataStructure(string propertyName, string prefix, int index)
    {
        MarkItems.Add(new MarkItem(propertyName, prefix, index));
    }

    public void MarkDataStructure(string propertyName)
    {
        MarkItems.Add(new MarkItem(propertyName, string.Empty, -1));
    }


    public List<MarkItem> MarkItems { get; } = new();
}