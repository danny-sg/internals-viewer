
namespace InternalsViewer.Internals;

public class MarkItem(string propertyName, int startPosition, int length)
{
    public MarkItem(string propertyName, int startPosition, int length, int index)
        : this(propertyName, startPosition, length)
    {
        Index = index;
    }

    public MarkItem(string propertyName, string prefix, int startPosition, int length, int index)
        : this(propertyName, startPosition, length, index)
    {
        Prefix = prefix;
    }

    public MarkItem(string propertyName, string prefix, int index) : this(propertyName, -1, 0)
    {
        Prefix = prefix;
        Index = index;
    }

    public int StartPosition { get; set; } = startPosition;

    public int Length { get; set; } = length;

    public string PropertyName { get; set; } = propertyName;

    public int Index { get; set; } = -1;

    public string Prefix { get; set; }
}