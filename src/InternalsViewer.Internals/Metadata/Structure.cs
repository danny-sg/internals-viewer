namespace InternalsViewer.Internals.Metadata;

public abstract class Structure<T>(long allocationUnitId) where T : ColumnStructure
{
    public int ObjectId { get; set; }

    public int IndexId { get; set; }

    public long AllocationUnitId { get; set; } = allocationUnitId;

    public List<T> Columns { get; set; } = new();

    public bool HasSparseColumns => Columns.Any(c => c.IsSparse);
}