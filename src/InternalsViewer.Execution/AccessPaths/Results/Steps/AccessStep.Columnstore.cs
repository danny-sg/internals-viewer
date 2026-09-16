using InternalsViewer.Execution.AccessPaths.Predicates;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    /// <summary>
    /// A compressed data filter compiled into a bitmap of qualifying dictionary ids for a rowgroup
    /// </summary>
    public sealed record CompressedDataFilterBitmap(int RowGroupId,
                                                    int ColumnId,
                                                    string ColumnName,
                                                    CompressedFilterCategory Category,
                                                    IReadOnlyList<long> DictionaryIds,
                                                    IReadOnlyList<bool> Qualifies,
                                                    IReadOnlyList<string> Values,
                                                    int QualifyingCount) : AccessStep(AccessPhase.RowGroup)
    {
        public int EntryCount => DictionaryIds.Count;
    }

    public sealed record PartitionSkipped(long PartitionId, string Reason) : AccessStep(AccessPhase.Partition);

    public sealed record SegmentElimination(int RowGroupId, int EliminatedCount, int SegmentCount)
        : AccessStep(AccessPhase.SegmentElimination);

    public sealed record RowGroupSkipped(int RowGroupId, string Reason) : AccessStep(AccessPhase.RowGroup);

    public sealed record SegmentSkipped(int RowGroupId, int ColumnId, string ColumnName, string Reason)
        : AccessStep(AccessPhase.SegmentElimination);

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

        public bool HasPredicate { get; init; }

        public int PureColumns { get; init; }

        public int ImpureColumns { get; init; }
    }

    public sealed record AggregateLocalMerge(int RowGroupId,
                                            long LocalGroups,
                                            long GlobalGroupsBefore,
                                            long GlobalGroupsAfter,
                                            long Materialised) : AccessStep(AccessPhase.Accumulate)
    {
        public long NewGroups => GlobalGroupsAfter - GlobalGroupsBefore;

        public bool HasRowGroup => RowGroupId >= 0;
    }

    public sealed record AggregatePushdown(int RowGroupId,
                                          int FirstRow,
                                          int RowCount,
                                          long Groups,
                                          long NewGroups,
                                          bool IsRunFolded) : AccessStep(AccessPhase.Accumulate);

    public sealed record BatchSkipped(int RowGroupId,
                                      int FirstRow,
                                      int RowCount) : AccessStep(AccessPhase.Walk)
    {
        public int FilterRleEntries { get; init; }

        public int FilterOperations { get; init; }

        public bool HasCompressedFilter { get; init; }

        public bool HasPredicate { get; init; }
    }

    public sealed record ComputeVector(long Number, int RowGroupId, string Columns, int RowCount)
        : AccessStep(AccessPhase.Compute);

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
