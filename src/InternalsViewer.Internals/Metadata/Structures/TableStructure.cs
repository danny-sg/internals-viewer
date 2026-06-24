namespace InternalsViewer.Internals.Metadata.Structures;

public sealed record TableStructure(long AllocationUnitId)
    : Structure<ColumnStructure>(AllocationUnitId)
{
    public int ColumnCount => Columns.Count;
}