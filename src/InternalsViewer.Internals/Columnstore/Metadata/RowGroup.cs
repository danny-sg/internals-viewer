using InternalsViewer.Internals.Columnstore.Metadata.Enums;

namespace InternalsViewer.Internals.Columnstore.Metadata;

/// <summary>
/// Columnstore table rowgroup
/// </summary>
/// <remarks>
/// A rowgroup is a set of rows in a columnstore table/index.
///
/// One index will have multiple rowgroups, the maximum number of rows per rowgroup is 1,048,576 (2^20)
///
/// Each rowgroup has one column segment per column that is compressed for the data profile in that column/rowgroup. The idea is these
/// 'chunks' of rows are managable and a good balance for compression + bulk operations vs performance.
/// </remarks>
public sealed class RowGroup
{
    public long HobtId { get; set; }

    public long PartitionId { get; set; }

    public int RowGroupId { get; set; }

    public RowGroupState State { get; set; }

    public int RawStatus { get; set; }

    public int Version { get; set; }

    public int Flags { get; set; }

    public int CompressedReason { get; set; }

    public long Generation { get; set; }

    public int TotalRows { get; set; }

    public int? DeletedRows { get; set; }

    public long DeltaStoreHobtId { get; set; }

    public DateTime? CreatedTime { get; set; }

    public DateTime? ClosedTime { get; set; }

    public RowGroupMetadataPointer MetadataBlob { get; set; }

    public List<ColumnSegment> Segments { get; } = [];

    public long SizeInBytes => Segments.Sum(s => s.OnDiskSize);

    public bool IsCompressed => State == RowGroupState.Compressed;

    public bool IsDeltaStore => State is RowGroupState.Open or RowGroupState.Closed;

    public bool HasExpectedSegments => IsCompressed == Segments.Count > 0;
}