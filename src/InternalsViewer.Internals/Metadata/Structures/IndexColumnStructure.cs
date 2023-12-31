namespace InternalsViewer.Internals.Metadata.Structures;

public record IndexColumnStructure : ColumnStructure
{
    public bool IsKey { get; set; }

    public bool IsIncludeColumn { get; set; }

    public int IndexColumnId { get; set; }

    public short NodeOffset { get; set; }
}