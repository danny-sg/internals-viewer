namespace InternalsViewer.Internals.Engine.Annotations;

/// <summary>
/// Data Structure annotations that can be made against a <see cref="DataStructure">Data Structure</see> object
/// </summary>
public class DataStructureItem(string propertyName, int startPosition, int length)
{
    public DataStructureItem(string propertyName, int startPosition, int length, int index)
        : this(propertyName, startPosition, length)
    {
        Index = index;
    }

    public DataStructureItem(string propertyName, string prefix, int startPosition, int length, int index)
        : this(propertyName, startPosition, length, index)
    {
        Prefix = prefix;
    }

    public DataStructureItem(string propertyName, string prefix, int index) : this(propertyName, -1, 0)
    {
        Prefix = prefix;
        Index = index;
    }

    public int StartPosition { get; } = startPosition;

    public int Length { get; } = length;

    public string PropertyName { get; } = propertyName;

    public int Index { get; } = -1;

    public string Prefix { get; } = string.Empty;

    public bool IsVirtual { get; set; }
}