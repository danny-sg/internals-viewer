using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryRegion(int Offset, int Length)
{
    public static List<MemoryHistoryRegion> Merge(IEnumerable<MemoryHistoryRegion> regions)
        => [.. IntervalHelper.Coalesce(regions, r => r.Offset, r => r.Offset + r.Length)
                        .Select(r => new MemoryHistoryRegion(r.Start, r.End - r.Start))];
}
