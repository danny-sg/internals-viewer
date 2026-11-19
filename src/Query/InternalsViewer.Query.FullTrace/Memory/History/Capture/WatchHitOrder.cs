using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class WatchHitOrder
{
    private WatchHitOrder(WatchHitStore hits, (ChunkHit Hit, byte[] Data)[] sorted)
    {
        Hits = hits;
        Sorted = sorted;
    }

    private WatchHitStore Hits { get; }

    private (ChunkHit Hit, byte[] Data)[] Sorted { get; }

    public static WatchHitOrder Get(WatchResult watched)
    {
        var hits = watched.Hits;

        return hits.IsOrdered || hits.Streams.TrueForAll(s => s.IsOrdered) ? new WatchHitOrder(hits, []) : GetSorted(hits);
    }

    public IEnumerable<(ChunkHit Hit, byte[] Data)> GetHits()
    {
        if (Sorted.Length > 0)
        {
            return Sorted;
        }

        return Hits.IsOrdered ? GetArrivalHits(Hits) : GetMergedHits(Hits);
    }

    private static WatchHitOrder GetSorted(WatchHitStore hits)
    {
        var sorted = new (ChunkHit Hit, byte[] Data)[hits.Count];

        var keys = new (TracePosition Position, int Arrival)[hits.Count];

        var arrival = 0;

        foreach (var hit in GetArrivalHits(hits))
        {
            sorted[arrival] = hit;

            keys[arrival] = (hit.Hit.Position, arrival);

            arrival++;
        }

        Array.Sort(keys, sorted);

        return new WatchHitOrder(hits, sorted);
    }

    private static IEnumerable<(ChunkHit Hit, byte[] Data)> GetArrivalHits(WatchHitStore hits)
    {
        var readers = hits.Streams.ConvertAll(s => s.GetReader());

        foreach (var (stream, count) in hits.Runs)
        {
            for (var index = 0; index < count; index++)
            {
                yield return readers[stream].Read();
            }
        }
    }

    private static IEnumerable<(ChunkHit Hit, byte[] Data)> GetMergedHits(WatchHitStore hits)
    {
        var readers = hits.Streams.ConvertAll(s => s.GetReader());

        var current = new (ChunkHit Hit, byte[] Data)[readers.Count];

        var queue = new PriorityQueue<int, TracePosition>(readers.Count);

        for (var stream = 0; stream < readers.Count; stream++)
        {
            current[stream] = readers[stream].Read();

            queue.Enqueue(stream, current[stream].Hit.Position);
        }

        while (queue.TryDequeue(out var stream, out _))
        {
            yield return current[stream];

            if (readers[stream].TryRead(out var hit, out var data))
            {
                current[stream] = (hit, data);

                queue.Enqueue(stream, hit.Position);
            }
        }
    }
}
