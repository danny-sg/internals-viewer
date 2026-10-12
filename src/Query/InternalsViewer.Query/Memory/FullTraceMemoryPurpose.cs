namespace InternalsViewer.Query.Memory;

public sealed record FullTraceMemoryPurpose(string Name,
                                            ulong Allocated,
                                            long Allocations,
                                            ulong Freed,
                                            ulong PeakInUse,
                                            IReadOnlyList<FullTraceMemoryUse> Uses)
{
    public ulong Held => Allocated > Freed ? Allocated - Freed : 0;

    public IReadOnlyList<FullTraceMemoryPurpose> Kinds { get; init; } = [];
}
