using System.Diagnostics.CodeAnalysis;

namespace InternalsViewer.Query.FullTrace.Timeline;

internal sealed class TraceSpanIdIndex
{
    private TraceSpanIdIndex(TraceTimelineRow[] rows, int[] starts, int[] firstRows)
    {
        Rows = rows;
        Starts = starts;
        FirstRows = firstRows;
    }

    private TraceTimelineRow[] Rows { get; }

    private int[] Starts { get; }

    private int[] FirstRows { get; }

    public static TraceSpanIdIndex Build(IReadOnlyList<TraceTimelineThread> threads)
    {
        var rows = new List<TraceTimelineRow>();

        var starts = new List<int>();

        var firstRows = new int[threads.Count];

        var next = 0;

        for (var lane = 0; lane < threads.Count; lane++)
        {
            firstRows[lane] = rows.Count;

            foreach (var row in threads[lane].Rows)
            {
                rows.Add(row);

                starts.Add(next);

                next = checked(next + row.Count);
            }
        }

        starts.Add(next);

        return new TraceSpanIdIndex([.. rows], [.. starts], firstRows);
    }

    public int GetId(int lane, int depth, int index) => Starts[FirstRows[lane] + depth] + index;

    public bool TryGet(int id, [NotNullWhen(true)] out TraceTimelineRow? row, out int index)
    {
        if (id < 0 || id >= Starts[^1])
        {
            row = null;

            index = -1;

            return false;
        }

        var low = 0;

        var high = Starts.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (Starts[middle] <= id)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        row = Rows[low - 1];

        index = id - Starts[low - 1];

        return true;
    }
}
