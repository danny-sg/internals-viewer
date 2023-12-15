namespace InternalsViewer.Internals.Metadata;

public class IndexColumnStructure : ColumnStructure
{
    public bool Key { get; set; }

    public bool IncludedColumn { get; set; }

    public int IndexColumnId { get; set; }
}