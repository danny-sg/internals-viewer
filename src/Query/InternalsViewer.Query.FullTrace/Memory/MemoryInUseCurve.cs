using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryInUseCurve(TracePosition[] positions, ulong[] values)
{
    public static MemoryInUseCurve Empty { get; } = new([], []);

    public bool IsEmpty => Positions.Length == 0;

    internal long Bytes => Positions.Length * (long)sizeof(ulong) + Values.Length * (long)sizeof(ulong);

    private TracePosition[] Positions { get; } = positions;

    private ulong[] Values { get; } = values;

    public ulong GetValue(TracePosition position)
    {
        var index = SortedSearch.FirstAfter<TracePosition>(Positions, position) - 1;

        return index >= 0 ? Values[index] : 0;
    }

    public ulong PeakDuring(TracePosition start, TracePosition end)
    {
        var peak = GetValue(start);

        for (var index = SortedSearch.FirstAfter<TracePosition>(Positions, start);
             index < Positions.Length && Positions[index] < end;
             index++)
        {
            peak = Math.Max(peak, Values[index]);
        }

        return peak;
    }
}
