namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal sealed record PlanNodeLayout
{
    public static PlanNodeLayout SqlServer2022 { get; } = new()
    {
        RowIteratorPlanObjectOffset = 0x48,
        BatchIteratorPlanObjectOffset = 0x10,
        BatchPlanObjectOffset = 0x10,
        PlanNodeIdOffset = 0x0C,
        PlanQueryOffset = 0x28
    };

    public static PlanNodeLayout SqlServer2025 { get; } = new()
    {
        RowIteratorPlanObjectOffset = 0x10,
        BatchIteratorPlanObjectOffset = 0x10,
        BatchPlanObjectOffset = 0x10,
        PlanNodeIdOffset = 0x0C,
        PlanQueryOffset = 0x28
    };

    public required ulong RowIteratorPlanObjectOffset { get; init; }

    public required ulong BatchIteratorPlanObjectOffset { get; init; }

    public required ulong BatchPlanObjectOffset { get; init; }

    public required ulong PlanNodeIdOffset { get; init; }

    public required ulong PlanQueryOffset { get; init; }
}
