using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Columnstore.Metadata;

public sealed class ColumnStoreIndex
{
    public long HobtId { get; set; }

    public int ObjectId { get; set; }

    public int IndexId { get; set; }

    public string? IndexName { get; set; }

    public string SchemaName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public bool IsClustered { get; set; }

    public List<ColumnStoreColumn> Columns { get; } = [];

    public List<RowGroup> RowGroups { get; } = [];

    /// <summary>
    /// Row sets backing the index, being the columnstore plus the delete bitmap and any delta stores
    /// </summary>
    public List<ColumnstoreRowset> Rowsets { get; } = [];

    public ColumnstoreRowset? ColumnStoreRowset
        => Rowsets.FirstOrDefault(o => o.RowsetType == ColumnstoreRowsetType.ColumnStore);

    /// <summary>
    /// Holds one row per row logically deleted from a compressed row group
    /// </summary>
    public ColumnstoreRowset? DeleteBitmap
        => Rowsets.FirstOrDefault(o => o.RowsetType == ColumnstoreRowsetType.DeleteBitmap);

    public IEnumerable<ColumnstoreRowset> DeltaStores
        => Rowsets.Where(o => o.RowsetType == ColumnstoreRowsetType.DeltaStore);

    public AllocationUnit? DataAllocationUnit => ColumnStoreRowset?.DataAllocationUnit;

    public AllocationUnit? BlobAllocationUnit => ColumnStoreRowset?.BlobAllocationUnit;

    public PageAddress FirstPage => BlobAllocationUnit?.FirstPage ?? PageAddress.Empty;

    public PageAddress RootPage => BlobAllocationUnit?.RootPage ?? PageAddress.Empty;

    public PageAddress FirstIamPage => BlobAllocationUnit?.FirstIamPage ?? PageAddress.Empty;

    public AllocationUnit? DeleteBitmapAllocationUnit => DeleteBitmap?.DataAllocationUnit;

    public IEnumerable<RowGroup> CompressedRowGroups
        => RowGroups.Where(r => r.State == RowGroupState.Compressed).OrderBy(r => r.RowGroupId);

    public long TotalRows => RowGroups.Sum(r => (long)r.TotalRows);

    public long TotalSize => RowGroups.Sum(r => r.SizeInBytes);
}
