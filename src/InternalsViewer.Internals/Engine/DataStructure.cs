using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine;

public class DataStructure
{
    public void MarkProperty(string propertyName, int startPosition, int length)
    {
        MarkItems.Add(new DataStructureItem(propertyName, startPosition, length));
    }

    public void MarkProperty(string propertyName, int startPosition, int length, int index)
    {
        MarkItems.Add(new DataStructureItem(propertyName, startPosition, length, index));
    }

    public void MarkProperty(string propertyName, string prefix, int startPosition, int length, int index)
    {
        MarkItems.Add(new DataStructureItem(propertyName, startPosition, length, index));
    }

    public void MarkProperty(string propertyName, string prefix, int index)
    {
        MarkItems.Add(new DataStructureItem(propertyName, prefix, index));
    }

    public void MarkProperty(string propertyName)
    {
        MarkItems.Add(new DataStructureItem(propertyName, string.Empty, -1));
    }

    public void MarkVirtualProperty(string propertyName)
    {
        MarkItems.Add(new DataStructureItem(propertyName, string.Empty, -1) { IsVirtual = true });
    }

    public List<DataStructureItem> MarkItems { get; } = new();
}