namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

public sealed class TimeTravelInUseCurve(double[] positions, ulong[] values)
{
    public static TimeTravelInUseCurve Empty { get; } = new([], []);

    public bool IsEmpty => Positions.Length == 0;

    private double[] Positions { get; } = positions;

    private ulong[] Values { get; } = values;

    public ulong PeakDuring(double start, double end)
    {
        var index = LastAtOrBefore(start);

        var peak = index >= 0 ? Values[index] : 0;

        for (var next = index + 1; next < Positions.Length && Positions[next] < end; next++)
        {
            peak = Math.Max(peak, Values[next]);
        }

        return peak;
    }

    private int LastAtOrBefore(double position)
    {
        var low = 0;

        var high = Positions.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (Positions[middle] <= position)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low - 1;
    }
}
