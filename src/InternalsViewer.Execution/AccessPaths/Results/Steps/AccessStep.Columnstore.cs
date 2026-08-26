namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record PartitionSkipped(long PartitionId, string Reason) : AccessStep(AccessPhase.Partition);

    public sealed record SegmentElimination(int RowGroupId, int EliminatedCount, int SegmentCount)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record RowGroupSkipped(int RowGroupId, string Reason) : AccessStep(AccessPhase.RowGroup);

    public sealed record SegmentSkipped(int RowGroupId, int ColumnId, string ColumnName, string Reason)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record SegmentOpened(int RowGroupId, int ColumnId, string ColumnName, long SizeBytes)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record RowGroupOpened(int RowGroupId, int ColumnCount, int BatchRows)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record DeleteBitmapApplied(int RowGroupId, int Count) : AccessStep(AccessPhase.Walk);

    public sealed record BatchProduced(long Number, int RowGroupId, int FirstRow, int RowCount, int QualifyingCount)
        : AccessStep(AccessPhase.Walk);
}
