using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Controls.Columnstore;

public readonly struct RunValueScale
{
    private RunValueScale(long minimum, long maximum, int lowest, int highest)
    {
        Minimum = minimum;

        Maximum = maximum;

        Lowest = lowest;

        Highest = highest;
    }

    private long Minimum { get; }

    private long Maximum { get; }

    private int Lowest { get; }

    private int Highest { get; }

    public static RunValueScale Build(IReadOnlyList<RleEntry> runs, int lowest, int highest)
    {
        var minimum = long.MaxValue;

        var maximum = long.MinValue;

        foreach (var run in runs)
        {
            if (!run.IsValue || run.IsTerminator)
            {
                continue;
            }

            minimum = Math.Min(minimum, run.Value);

            maximum = Math.Max(maximum, run.Value);
        }

        return new RunValueScale(minimum, maximum, lowest, highest);
    }

    public int GetAlpha(RleEntry run)
    {
        if (run.IsTerminator)
        {
            return 0;
        }

        if (!run.IsValue)
        {
            return BitpackAlpha;
        }

        if (Maximum <= Minimum)
        {
            return (Lowest + Highest) / 2;
        }

        var position = (double)(run.Value - Minimum) / (Maximum - Minimum);

        return Lowest + (int)(position * (Highest - Lowest));
    }

    private const int BitpackAlpha = 40;
}
