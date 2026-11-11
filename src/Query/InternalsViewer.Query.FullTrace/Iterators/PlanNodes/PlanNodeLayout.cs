namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal sealed record PlanNodeLayout
{
    public static PlanNodeLayout SqlServer2022 { get; } = new()
    {
        RowIteratorPlanObjectOffset = 0x48,
        BatchIteratorPlanObjectOffset = 0x10,
        BatchPlanObjectOffset = 0x10,
        PlanNodeIdOffset = 0x0C,
        PlanQueryOffset = 0x28,
        PlanOutputsOffset = 0x30,
        OutputCountOffset = 0x08,
        OutputColumnsOffset = 0x10,
        ColumnTypeOffset = 0x30,
        ColumnTypeDetailsOffset = 0x40,
        ColumnProducerOffset = 0x50,
        StorageColumnProducerOffset = 0x30,
        StorageColumnTypeOffset = 0x38,
        StorageColumnTypeDetailsOffset = 0x48
    };

    public static PlanNodeLayout SqlServer2025 { get; } = new()
    {
        RowIteratorPlanObjectOffset = 0x10,
        BatchIteratorPlanObjectOffset = 0x10,
        BatchPlanObjectOffset = 0x10,
        PlanNodeIdOffset = 0x0C,
        PlanQueryOffset = 0x28,
        PlanOutputsOffset = 0x30,
        OutputCountOffset = 0x08,
        OutputColumnsOffset = 0x10,
        ColumnTypeOffset = 0x30,
        ColumnTypeDetailsOffset = 0x40,
        ColumnProducerOffset = 0x50,
        StorageColumnProducerOffset = 0x30,
        StorageColumnTypeOffset = 0x38,
        StorageColumnTypeDetailsOffset = 0x48
    };

    public required ulong RowIteratorPlanObjectOffset { get; init; }

    public required ulong BatchIteratorPlanObjectOffset { get; init; }

    public required ulong BatchPlanObjectOffset { get; init; }

    public required ulong PlanNodeIdOffset { get; init; }

    public required ulong PlanQueryOffset { get; init; }

    public required ulong PlanOutputsOffset { get; init; }

    public required ulong OutputCountOffset { get; init; }

    public required ulong OutputColumnsOffset { get; init; }

    public required ulong ColumnTypeOffset { get; init; }

    public required ulong ColumnTypeDetailsOffset { get; init; }

    public required ulong ColumnProducerOffset { get; init; }

    public required ulong StorageColumnProducerOffset { get; init; }

    public required ulong StorageColumnTypeOffset { get; init; }

    public required ulong StorageColumnTypeDetailsOffset { get; init; }
}
