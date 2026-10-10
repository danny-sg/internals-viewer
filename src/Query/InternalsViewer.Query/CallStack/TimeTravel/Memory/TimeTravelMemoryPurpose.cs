namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public sealed record TimeTravelMemoryPurpose(string Name,
                                             ulong Allocated,
                                             long Allocations,
                                             ulong Freed,
                                             ulong PeakInUse,
                                             IReadOnlyList<TimeTravelMemoryUse> Uses)
{
    public ulong Held => Allocated > Freed ? Allocated - Freed : 0;

    public IReadOnlyList<TimeTravelMemoryPurpose> Kinds { get; init; } = [];
}
