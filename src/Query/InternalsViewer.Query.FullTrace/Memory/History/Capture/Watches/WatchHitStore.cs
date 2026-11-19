using InternalsViewer.Query.FullTrace.Replay.Native;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;

internal sealed class WatchHitStore
{
    public int Count { get; private set; }

    public bool IsOrdered { get; private set; } = true;

    public List<WatchHitStream> Streams { get; } = [];

    public List<(int Stream, int Count)> Runs { get; } = [];

    private Dictionary<uint, int> StreamIndexes { get; } = [];

    private TracePosition Last { get; set; }

    public void Add(in WatchHit hit, ReadOnlySpan<byte> bytes)
    {
        var stream = Runs.Count > 0 && Streams[Runs[^1].Stream].Thread == hit.Thread ? Runs[^1].Stream : GetStream(hit.Thread);

        Streams[stream].Add(hit, bytes);

        if (Runs.Count > 0 && Runs[^1].Stream == stream)
        {
            Runs[^1] = (stream, Runs[^1].Count + 1);
        }
        else
        {
            Runs.Add((stream, 1));
        }

        var position = hit.Position;

        if (position < Last)
        {
            IsOrdered = false;
        }

        Last = position;

        Count++;
    }

    private int GetStream(uint thread)
    {
        if (!StreamIndexes.TryGetValue(thread, out var stream))
        {
            stream = Streams.Count;

            StreamIndexes[thread] = stream;

            Streams.Add(new WatchHitStream(thread));
        }

        return stream;
    }
}
