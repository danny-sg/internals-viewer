namespace InternalsViewer.Internals.Engine.Annotations;

/// <summary>
/// Data Structure annotations that can be made against a <see cref="DataStructure">Data Structure</see> object
/// </summary>
public class DataStructureItem
{
    public ItemType ItemType { get; set; }

    public int Offset { get; set; } = -1;

    public int Length { get; set; } = -1;

    public string Name { get; set; } = string.Empty;

    public int Index { get; set; } = -1;

    public string Prefix { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public bool IsVisible { get; set; } = true;
}

public class PropertyItem : DataStructureItem
{
    public string PropertyName { get; set; } = string.Empty;
}

public class ValueItem : DataStructureItem
{
    public object? Value { get; set; }
}