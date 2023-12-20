using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine;

public class DataStructure
{
    public void MarkDataStructure(string propertyName, int startPosition, int length)
    {
        MarkItems.Add(new DataStructureItem(propertyName, startPosition, length));
    }

    public void MarkDataStructure(string propertyName, int startPosition, int length, int index)
    {
        MarkItems.Add(new DataStructureItem(propertyName, startPosition, length, index));
    }

    public void MarkDataStructure(string propertyName, string prefix, int startPosition, int length, int index)
    {
        MarkItems.Add(new DataStructureItem(propertyName, startPosition, length, index));
    }

    public void MarkDataStructure(string propertyName, string prefix, int index)
    {
        MarkItems.Add(new DataStructureItem(propertyName, prefix, index));
    }

    public void MarkDataStructure(string propertyName)
    {
        MarkItems.Add(new DataStructureItem(propertyName, string.Empty, -1));
    }


    public List<DataStructureItem> MarkItems { get; } = new();
}