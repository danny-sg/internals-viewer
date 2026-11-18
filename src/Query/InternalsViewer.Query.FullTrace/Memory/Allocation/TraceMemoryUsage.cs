using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

public sealed record TraceMemoryUsage(string Name, int Allocations, ulong AllocatedBytes, ulong PeakBytes, MemoryUsageCurve InUse)
{
    public const string TotalName = "Total";

    public const string OtherName = "Other";

    public static List<TraceMemoryUsage> Build(MemoryHistoryStore store, IReadOnlyList<(int Block, string? Category)> blocks)
    {
        List<TraceMemoryUsage> categories =
        [
            .. blocks.GroupBy(b => b.Category ?? OtherName)
                     .Select(g => Create(g.Key, store, g.Select(b => b.Block)))
                     .OrderBy(u => u.Name == OtherName)
                     .ThenByDescending(u => u.AllocatedBytes)
        ];

        return [Create(TotalName, store, blocks.Select(b => b.Block)), .. categories];
    }

    private static TraceMemoryUsage Create(string name, MemoryHistoryStore store, IEnumerable<int> blocks)
    {
        var changes = new List<(TracePosition Position, long Bytes)>();

        var allocations = 0;

        var allocated = 0UL;

        foreach (var block in blocks)
        {
            var info = store.Blocks[block];

            var instances = store.GetInstances(block);

            IEnumerable<(TracePosition From, TracePosition To)> lives = instances.Count > 0
                ? instances.Select(i => (i.FromPosition, i.ToPosition))
                : [(info.FromPosition, info.ToPosition)];

            foreach (var (from, to) in lives)
            {
                allocations++;

                allocated += (ulong)info.Size;

                changes.Add((from, info.Size));

                if (to.IsKnown)
                {
                    changes.Add((to, -info.Size));
                }
            }
        }

        changes.Sort((left, right) => left.Position.CompareTo(right.Position));

        var positions = new List<TracePosition>(changes.Count);

        var values = new List<ulong>(changes.Count);

        var current = 0L;

        var peak = 0UL;

        foreach (var (position, bytes) in changes)
        {
            current = Math.Max(0, current + bytes);

            if (positions.Count > 0 && positions[^1] == position)
            {
                values[^1] = (ulong)current;
            }
            else
            {
                positions.Add(position);

                values.Add((ulong)current);
            }

            peak = Math.Max(peak, (ulong)current);
        }

        return new TraceMemoryUsage(name, allocations, allocated, peak, new MemoryUsageCurve([.. positions], [.. values]));
    }
}
