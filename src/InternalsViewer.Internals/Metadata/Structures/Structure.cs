using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Records.CdRecordType;

namespace InternalsViewer.Internals.Metadata.Structures;

public abstract record Structure<T>(long AllocationUnitId) where T : ColumnStructure
{
    public int ObjectId { get; set; }

    public int IndexId { get; set; }

    public long AllocationUnitId { get; set; } = AllocationUnitId;

    public long PartitionId { get; set; }

    public List<T> Columns { get; set; } = new();

    public bool HasSparseColumns => Columns.Any(c => c.IsSparse);

    public IndexType IndexType { get; set; }

    public CompressionType CompressionType { get; set; }
}