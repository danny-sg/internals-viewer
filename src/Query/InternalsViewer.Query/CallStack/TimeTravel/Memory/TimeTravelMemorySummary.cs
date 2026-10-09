namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public sealed record TimeTravelMemorySummary(ulong Bytes,
                                             long Allocations,
                                             long Returned,
                                             long Frees,
                                             long MatchedFrees,
                                             ulong PeakInUse,
                                             IReadOnlyList<TimeTravelMemoryPurpose> Purposes);
