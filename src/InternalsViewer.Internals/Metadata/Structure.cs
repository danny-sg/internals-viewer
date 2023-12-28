namespace InternalsViewer.Internals.Metadata;

public abstract class Structure(long allocationUnitId)
{
    public int ObjectId { get; set; }

    public int IndexId { get; set; }

    public long AllocationUnitId { get; set; } = allocationUnitId;

    public List<ColumnStructure> Columns { get; set; } = new();

    public bool HasSparseColumns => Columns.Any(c => c.IsSparse);
}