namespace InternalsViewer.Query.FullTrace.Timeline;

internal sealed class TraceInnermostCallIndex(TraceTimeline? timeline)
{
    private Dictionary<uint, int> Lanes { get; } = timeline is null
        ? []
        : timeline.Threads.Select((t, i) => (t.ThreadId, Lane: i)).ToDictionary(t => t.ThreadId, t => t.Lane);

    private Dictionary<uint, int> Depths { get; } = [];

    public (int CallNodeIndex, int CallIndex) GetCall(uint thread, TracePosition position)
    {
        if (!TryFind(thread, position, out var lane, out var depth, out var index))
        {
            return (-1, -1);
        }

        var row = timeline!.Threads[lane].Rows[depth];

        return (row.GetCallNodeIndex(index), row.GetCallIndex(index));
    }

    public int GetSpanId(uint thread, TracePosition position) => GetSpanId(thread, position, out _);

    public int GetSpanId(uint thread, TracePosition position, out TracePosition end)
    {
        if (!TryFind(thread, position, out var lane, out var depth, out var index))
        {
            end = TracePosition.Unknown;

            return -1;
        }

        end = timeline!.Threads[lane].Rows[depth].GetToPosition(index);

        return timeline.GetSpanId(lane, depth, index);
    }

    private bool TryFind(uint thread, TracePosition position, out int lane, out int depth, out int index)
    {
        index = -1;

        depth = -1;

        if (!Lanes.TryGetValue(thread, out lane) || timeline!.Threads[lane].Rows.Count == 0)
        {
            return false;
        }

        var rows = timeline.Threads[lane].Rows;

        depth = Depths.TryGetValue(thread, out var cached) ? Math.Min(cached, rows.Count - 1) : 0;

        while (depth >= 0 && (index = rows[depth].GetIndex(position)) < 0)
        {
            depth--;
        }

        if (depth < 0)
        {
            return false;
        }

        while (depth + 1 < rows.Count && rows[depth + 1].GetIndex(position) is >= 0 and var deeper)
        {
            depth++;

            index = deeper;
        }

        Depths[thread] = depth;

        return true;
    }
}
