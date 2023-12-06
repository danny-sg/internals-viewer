
namespace InternalsViewer.Internals;

public class MarkItem
{
    public MarkItem(string propertyName, int startPosition, int length)
    {
        PropertyName = propertyName;
        StartPosition = startPosition;
        Length = length;
    }

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

    public MarkItem(string propertyName, string prefix, int index)
    {
        PropertyName = propertyName;
        Prefix = prefix;
        Index = index;
        StartPosition = -1;

    }

    public int StartPosition { get; set; }

    public int Length { get; set; }

    public string PropertyName { get; set; }

    public int Index { get; set; } = -1;

    public string Prefix { get; set; }
}