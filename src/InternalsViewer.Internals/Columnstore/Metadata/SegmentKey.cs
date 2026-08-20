namespace InternalsViewer.Internals.Columnstore.Metadata;

public readonly record struct SegmentKey(long HobtId,
                                         long PartitionId,
                                         int RowGroupId,
                                         int ColumnId);