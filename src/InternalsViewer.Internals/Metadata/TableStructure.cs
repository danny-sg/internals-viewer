namespace InternalsViewer.Internals.Metadata;

public class TableStructure(long allocationUnitId) 
    : Structure<ColumnStructure>(allocationUnitId)
{
    public int ColumnCount => Columns.Count;
}