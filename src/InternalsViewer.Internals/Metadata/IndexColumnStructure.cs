namespace InternalsViewer.Internals.Metadata;

public class IndexColumnStructure : ColumnStructure
{
    public bool IsKey { get; set; }

    public bool IsIncludeColumn { get; set; }

    public int IndexColumnId { get; set; }
}