namespace InternalsViewer.Internals.Metadata.Structures;

public sealed record IndexStructure(long AllocationUnitId)
    : Structure<IndexColumnStructure>(AllocationUnitId)
{
    public bool IsUnique { get; set; }

    public bool HasFilter { get; set; }

    public TableStructure? TableStructure { get; set; }

    public List<IndexColumnStructure> KeyColumns => field ??= Columns.Where(c => c.IsKey || c.IsUniqueifier).ToList();

    public List<IndexColumnStructure> IndexKeyColumns => field ??= Columns.Where(c => c.IsIndexKey).ToList();
}