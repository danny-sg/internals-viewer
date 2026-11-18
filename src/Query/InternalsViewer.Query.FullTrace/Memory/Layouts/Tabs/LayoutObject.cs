using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;

public sealed class LayoutObject(string name, string type, ulong address, int size, MemoryTabLayout layout, List<MemoryField> fields)
{
    public string Name { get; } = name;

    public string Type { get; } = type;

    public ulong Address { get; } = address;

    public int Size { get; } = size;

    public MemoryTabLayout Layout { get; } = layout;

    public List<MemoryField> Fields { get; } = fields;

    public int Stride { get; init; }

    public List<ulong?[]> Rows { get; } = [];

    public List<LayoutObject> Children { get; } = [];

    public (LayoutObject Object, int Row)? Find(ulong address)
    {
        if (address < Address || address >= Address + (ulong)Size)
        {
            return null;
        }

        foreach (var child in Children)
        {
            if (child.Find(address) is { } found)
            {
                return found;
            }
        }

        var offset = (int)(address - Address);

        if (Layout == MemoryTabLayout.Row)
        {
            return Stride > 0 && offset / Stride < Rows.Count ? (this, offset / Stride) : null;
        }

        return (this, Fields.FindLastIndex(f => f.Offset <= offset && offset < f.Offset + f.Length));
    }
}
