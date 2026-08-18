namespace InternalsViewer.Internals.Engine.Columnstore;

public readonly record struct SegmentKey(long HobtId,
                                         long PartitionId,
                                         int RowGroupId,
                                         int ColumnId);