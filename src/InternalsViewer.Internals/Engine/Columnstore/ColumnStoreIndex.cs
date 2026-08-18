using InternalsViewer.Internals.Engine.Columnstore.Enums;

namespace InternalsViewer.Internals.Engine.Columnstore;

public class ColumnStoreIndex
{
    public long HobtId { get; set; }

    public int ObjectId { get; set; }

    public int IndexId { get; set; }

    public string? IndexName { get; set; }

    public bool IsClustered { get; set; }

    public List<ColumnStoreColumn> Columns { get; } = [];

    public List<RowGroup> RowGroups { get; } = [];

    public IEnumerable<RowGroup> CompressedRowGroups
        => RowGroups.Where(r => r.State == RowGroupState.Compressed);
}