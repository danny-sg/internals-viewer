namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public sealed record TimeTravelMemorySummary(ulong Bytes,
                                             long Allocations,
                                             ulong PeakInUse,
                                             IReadOnlyList<TimeTravelMemoryPurpose> Kinds);
