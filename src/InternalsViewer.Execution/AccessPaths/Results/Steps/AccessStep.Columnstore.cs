namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record PartitionSkipped(long PartitionId, string Reason) : AccessStep(AccessPhase.Partition);

    public sealed record SegmentElimination(int RowGroupId, int EliminatedCount, int SegmentCount)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record RowGroupSkipped(int RowGroupId, string Reason) : AccessStep(AccessPhase.RowGroup);

    public sealed record SegmentSkipped(int RowGroupId, int ColumnId, string ColumnName, string Reason)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record DictionaryOpened(int RowGroupId,
                                          int ColumnId,
                                          string ColumnName,
                                          bool IsGlobal,
                                          long EntryCount,
                                          long SizeBytes) : AccessStep(AccessPhase.RowGroup);

    public sealed record SegmentOpened(int RowGroupId, int ColumnId, string ColumnName, long SizeBytes)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record CompressedDataFilter(int RowGroupId, string Columns, bool OnCompressedData)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record RowGroupOpened(int RowGroupId, int ColumnCount, int BatchRows)
        : AccessStep(AccessPhase.RowGroup);

    public sealed record DeleteBitmapApplied(int RowGroupId, int Count) : AccessStep(AccessPhase.Walk);

    public sealed record BatchProduced(long Number,
                                      int RowGroupId,
                                      int FirstRow,
                                      int RowCount,
                                      int QualifyingCount) : AccessStep(AccessPhase.Walk)
    {
        public int FilterRleEntries { get; init; }

        public int FilterOperations { get; init; }

        public int Materialised { get; init; }

        public bool HasCompressedFilter { get; init; }
    }

    public sealed record FilterVector(long Number,
                                      int RowGroupId,
                                      string Columns,
                                      int RowsEvaluated,
                                      int Matches) : AccessStep(AccessPhase.Filter);

    public sealed record BatchFiltered(long Number,
                                       int RowGroupId,
                                       int RowCount,
                                       int QualifyingCount,
                                       long PassedCount) : AccessStep(AccessPhase.Walk);
}
