namespace InternalsViewer.Internals.Metadata;

public class TableStructure(long allocationUnitId) : Structure(allocationUnitId)
{
    public int ColumnCount => Columns.Count;
}