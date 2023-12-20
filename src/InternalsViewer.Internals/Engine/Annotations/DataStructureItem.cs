namespace InternalsViewer.Internals.Engine.Annotations;

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

    public int StartPosition { get; set; } = startPosition;

    public int Length { get; set; } = length;

    public string PropertyName { get; set; } = propertyName;

    public int Index { get; set; } = -1;

    public string Prefix { get; set; } = string.Empty;
}