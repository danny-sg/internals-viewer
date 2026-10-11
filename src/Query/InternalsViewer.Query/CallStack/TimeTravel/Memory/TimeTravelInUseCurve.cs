namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public sealed class TimeTravelInUseCurve(double[] positions, ulong[] values)
{
    public static TimeTravelInUseCurve Empty { get; } = new([], []);

    public bool IsEmpty => Positions.Length == 0;

    private double[] Positions { get; } = positions;

    private ulong[] Values { get; } = values;

    public ulong ValueAt(double position)
    {
        var index = SortedSearch.FirstAfter(Positions, position) - 1;

        return index >= 0 ? Values[index] : 0;
    }

    public ulong PeakDuring(double start, double end)
    {
        var peak = ValueAt(start);

        for (var index = SortedSearch.FirstAfter(Positions, start); index < Positions.Length && Positions[index] < end; index++)
        {
            peak = Math.Max(peak, Values[index]);
        }

        return peak;
    }
}
