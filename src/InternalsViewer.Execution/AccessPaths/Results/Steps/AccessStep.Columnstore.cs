namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record PartitionSkipped(long PartitionId, string Reason) : AccessStep(AccessPhase.Partition);

    public sealed record RowGroupSkipped(int RowGroupId, string Reason) : AccessStep(AccessPhase.RowGroup);

    public sealed record SegmentSkipped(int RowGroupId, int ColumnId, string ColumnName, string Reason)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record RowGroupOpened(int RowGroupId, int RowCount, int ColumnCount, int BatchRows)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record DeletedRowsSkipped(int RowGroupId, int Count) : AccessStep(AccessPhase.Walk);

    public sealed record BatchProduced(int RowGroupId, int FirstRow, int RowCount, int QualifyingCount)
        : AccessStep(AccessPhase.Walk);
}
