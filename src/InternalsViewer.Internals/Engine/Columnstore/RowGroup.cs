using InternalsViewer.Internals.Engine.Columnstore.Enums;

namespace InternalsViewer.Internals.Engine.Columnstore;

public sealed class RowGroup
{
    public long HobtId { get; set; }

    public long PartitionId { get; set; }

    /// <summary>
    /// Row group id. Held in syscsrowgroups.segment_id - the column is named
    /// segment_id for backward compatibility but identifies the row group.
    /// </summary>
    public int RowGroupId { get; set; }

    public RowGroupState State { get; set; }

    public int RawStatus { get; set; }

    public int Version { get; set; }

    public int Flags { get; set; }

    /// <summary>
    /// Why the row group closed below the maximum row count. Meaningful only
    /// for compressed row groups.
    /// </summary>
    public int CompressedReason { get; set; }

    public long Generation { get; set; }

    /// <summary>
    /// Rows in the row group, excluding deleted rows. For Open and Closed row
    /// groups these are held in the delta store, not in segments.
    /// </summary>
    public int TotalRows { get; set; }

    /// <summary>
    /// Deleted row count. Comes from the delete bitmap, which is a separate
    /// internal object - not populated from syscsrowgroups.
    /// </summary>
    public int? DeletedRows { get; set; }

    /// <summary>
    /// Heap holding the uncompressed rows for a delta row group. Zero once compressed.
    /// </summary>
    public long DeltaStoreHobtId { get; set; }

    public DateTime? CreatedTime { get; set; }

    public DateTime? ClosedTime { get; set; }

    /// <summary>
    /// Per-row-group metadata blob, distinct from the segment data blobs.
    /// </summary>
    public RowGroupMetadataPointer MetadataBlob { get; set; }

    public List<ColumnSegment> Segments { get; } = [];

    /// <summary>
    /// Summed from the segments - syscsrowgroups holds no size field.
    /// </summary>
    public long SizeInBytes => Segments.Sum(s => s.OnDiskSize);

    public bool IsCompressed => State == RowGroupState.Compressed;

    public bool IsDeltaStore => State is RowGroupState.Open or RowGroupState.Closed;

    /// <summary>
    /// Segments exist only once compressed. A delta row group with segments,
    /// or a compressed one without, indicates a parse problem.
    /// </summary>
    public bool HasExpectedSegments => IsCompressed == Segments.Count > 0;
}