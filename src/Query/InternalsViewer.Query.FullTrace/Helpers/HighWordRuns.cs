using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal sealed class HighWordRuns
{
    private HighWordRuns(int[] starts, uint[] values)
    {
        Starts = starts;
        Values = values;
    }

    public static HighWordRuns Empty { get; } = new([], []);

    internal long Bytes => Starts.Length * (long)(sizeof(int) + sizeof(uint));

    private int[] Starts { get; }

    private uint[] Values { get; }

    public static HighWordRuns Build(uint[] highs)
    {
        var starts = new List<int>();

        var values = new List<uint>();

        var current = 0u;

        for (var index = 0; index < highs.Length; index++)
        {
            if (highs[index] == current)
            {
                continue;
            }

            starts.Add(index);

            values.Add(highs[index]);

            current = highs[index];
        }

        return starts.Count == 0 ? Empty : new HighWordRuns([.. starts], [.. values]);
    }

    public uint Get(int index)
    {
        if (Starts.Length == 0)
        {
            return 0;
        }

        var run = SortedSearch.FirstAfter(Starts, index) - 1;

        return run >= 0 ? Values[run] : 0;
    }
}
