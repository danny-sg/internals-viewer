using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Allocation.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class WatchHitOrder
{
    private WatchHitOrder(WatchResult watched, int[] starts, int[] next, int[] heads, (TracePosition Position, long Location)[] sorted)
    {
        Watched = watched;
        Starts = starts;
        Next = next;
        Heads = heads;
        Sorted = sorted;
    }

    private WatchResult Watched { get; }

    private int[] Starts { get; }

    private int[] Next { get; }

    private int[] Heads { get; }

    private (TracePosition Position, long Location)[] Sorted { get; }

    public static WatchHitOrder Get(WatchResult watched)
        => IsOrdered(watched) ? new WatchHitOrder(watched, [], [], [], []) : GetMerged(watched);

    public IEnumerable<(ChunkHit Hit, byte[] Data)> GetHits()
    {
        if (Sorted.Length > 0)
        {
            return GetSortedHits();
        }

        return Heads.Length > 0 ? GetMergedHits() : Watched.Chunks.SelectMany(c => c.Hits.Select(h => (Hit: h, c.Data)));
    }

    private static WatchHitOrder GetMerged(WatchResult watched)
    {
        var chunks = watched.Chunks;

        var starts = new int[chunks.Count];

        var total = 0;

        for (var chunk = 0; chunk < chunks.Count; chunk++)
        {
            starts[chunk] = total;

            total += chunks[chunk].Hits.Length;
        }

        var next = new int[total];

        var lasts = new Dictionary<uint, (int Index, TracePosition Position)>();

        var heads = new List<int>();

        for (var chunk = 0; chunk < chunks.Count; chunk++)
        {
            var hits = chunks[chunk].Hits;

            for (var index = 0; index < hits.Length; index++)
            {
                var hit = hits[index];

                var global = starts[chunk] + index;

                next[global] = -1;

                if (lasts.TryGetValue(hit.Thread, out var last))
                {
                    if (hit.Position < last.Position)
                    {
                        return GetSorted(watched);
                    }

                    next[last.Index] = global;
                }
                else
                {
                    heads.Add(global);
                }

                lasts[hit.Thread] = (global, hit.Position);
            }
        }

        return new WatchHitOrder(watched, starts, next, [.. heads], []);
    }

    private static WatchHitOrder GetSorted(WatchResult watched)
    {
        var keys = new (TracePosition Position, long Location)[watched.HitCount];

        var at = 0;

        for (var chunk = 0; chunk < watched.Chunks.Count; chunk++)
        {
            var hits = watched.Chunks[chunk].Hits;

            for (var index = 0; index < hits.Length; index++)
            {
                keys[at++] = (hits[index].Position, (long)chunk << 32 | (uint)index);
            }
        }

        Array.Sort(keys);

        return new WatchHitOrder(watched, [], [], [], keys);
    }

    private static bool IsOrdered(WatchResult watched)
    {
        var last = default(TracePosition);

        foreach (var chunk in watched.Chunks)
        {
            foreach (var hit in chunk.Hits)
            {
                if (hit.Position < last)
                {
                    return false;
                }

                last = hit.Position;
            }
        }

        return true;
    }

    private IEnumerable<(ChunkHit Hit, byte[] Data)> GetMergedHits()
    {
        var queue = new PriorityQueue<int, TracePosition>(Heads.Length);

        foreach (var head in Heads)
        {
            queue.Enqueue(head, GetHit(head).Hit.Position);
        }

        while (queue.TryDequeue(out var global, out _))
        {
            yield return GetHit(global);

            if (Next[global] is >= 0 and var following)
            {
                queue.Enqueue(following, GetHit(following).Hit.Position);
            }
        }
    }

    private IEnumerable<(ChunkHit Hit, byte[] Data)> GetSortedHits()
    {
        foreach (var (_, location) in Sorted)
        {
            var chunk = Watched.Chunks[(int)(location >> 32)];

            yield return (chunk.Hits[(int)location], chunk.Data);
        }
    }

    private (ChunkHit Hit, byte[] Data) GetHit(int global)
    {
        var index = Array.BinarySearch(Starts, global);

        var chunk = index >= 0 ? index : ~index - 1;

        return (Watched.Chunks[chunk].Hits[global - Starts[chunk]], Watched.Chunks[chunk].Data);
    }
}
