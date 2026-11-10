using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

public sealed partial class AllocationIndex
{
    private AllocationIndex(Dictionary<uint, ThreadMemory> threads,
                            MemoryUsageCurve inUse,
                            MemoryUsageCurve workspace)
    {
        Threads = threads;
        InUse = inUse;
        Workspace = workspace;
    }

    public static AllocationIndex Empty { get; } = new([], MemoryUsageCurve.Empty, MemoryUsageCurve.Empty);

    public bool HasAllocations => Threads.Count > 0;

    public bool HasWorkspace => !Workspace.IsEmpty;

    private Dictionary<uint, ThreadMemory> Threads { get; }

    private MemoryUsageCurve InUse { get; }

    private MemoryUsageCurve Workspace { get; }

    internal static AllocationIndex Build(IEnumerable<MemoryAllocation> allocations, IEnumerable<MemoryFree> frees)
    {
        var kept = allocations.GroupBy(a => a.Thread).SelectMany(t => Outermost.Of(t, a => a.Start, a => a.End)).ToList();

        var released = frees.GroupBy(f => f.Thread).SelectMany(t => Outermost.Of(t, f => f.Start, f => f.End)).ToList();

        var events = kept.Select((a, index) => (a.Start, IsFree: false, Index: index))
                         .Concat(released.Select((f, index) => (f.Start, IsFree: true, Index: index)))
                         .OrderBy(e => e.Start)
                         .ThenBy(e => e.IsFree ? 0 : 1);

        var freedAt = kept.Select(a => a.Pointer == 0 ? a.Start : TracePosition.Unknown).ToArray();

        var threads = new Dictionary<uint, ThreadListMap>();

        var live = new Dictionary<ulong, int>();

        var positions = new List<TracePosition>();

        var values = new List<ulong>();

        var workspacePositions = new List<TracePosition>();

        var workspaceValues = new List<ulong>();

        ulong inUse = 0;

        ulong workspace = 0;

        foreach (var (start, isFree, index) in events)
        {
            if (isFree)
            {
                var free = released[index];

                if (!live.Remove(free.Pointer, out var allocation))
                {
                    continue;
                }

                freedAt[allocation] = start;

                GetLists(threads, free.Thread).Frees.Add((start, kept[allocation].Bytes));

                inUse -= Math.Min(inUse, kept[allocation].Bytes);

                if (kept[allocation].IsWorkspace)
                {
                    workspace -= Math.Min(workspace, kept[allocation].Bytes);

                    workspacePositions.Add(start);

                    workspaceValues.Add(workspace);
                }
            }
            else
            {
                if (kept[index].Pointer == 0)
                {
                    continue;
                }

                live[kept[index].Pointer] = index;

                inUse += kept[index].Bytes;

                if (kept[index].IsWorkspace)
                {
                    workspace += kept[index].Bytes;

                    workspacePositions.Add(start);

                    workspaceValues.Add(workspace);
                }
            }

            positions.Add(start);

            values.Add(inUse);
        }

        for (var index = 0; index < kept.Count; index++)
        {
            GetLists(threads, kept[index].Thread).Allocations.Add((kept[index], freedAt[index]));
        }

        var memory = threads.ToDictionary(t => t.Key, t => new ThreadMemory([.. t.Value.Allocations], [.. t.Value.Frees]));

        return new AllocationIndex(memory,
                               new MemoryUsageCurve([.. positions], [.. values]),
                               new MemoryUsageCurve([.. workspacePositions], [.. workspaceValues]));
    }

    public (ulong Bytes, int Count) GetAllocatedDuring(uint thread, TracePosition start, TracePosition end)
        => Threads.TryGetValue(thread, out var memory) ? memory.GetAllocatedDuring(start, end) : (0, 0);

    public ulong GetFreedDuring(uint thread, TracePosition start, TracePosition end)
        => Threads.TryGetValue(thread, out var memory) ? memory.GetFreedDuring(start, end) : 0;

    public IEnumerable<(TracePosition Position, ulong Bytes)> GetAllocations(uint thread)
        => Threads.TryGetValue(thread, out var memory) ? memory.GetAllocations() : [];

    public MemoryUsageCurve GetInUseWithin(uint thread, TracePosition start, TracePosition end)
        => Threads.TryGetValue(thread, out var memory) ? memory.GetInUseWithin(start, end) : MemoryUsageCurve.Empty;

    public IReadOnlyList<MemoryUsageCurve> GetInUseOwnedBy(uint thread, IReadOnlyList<IReadOnlyList<TraceSpan>> owners)
        => Threads.TryGetValue(thread, out var memory)
            ? memory.GetInUseOwnedBy(owners)
            : [.. owners.Select(_ => MemoryUsageCurve.Empty)];

    public ulong GetPeakInUseDuring(TracePosition start, TracePosition end) => InUse.GetPeakDuring(start, end);

    public ulong GetPeakWorkspaceDuring(TracePosition start, TracePosition end) => Workspace.GetPeakDuring(start, end);

    private static ThreadListMap GetLists(Dictionary<uint, ThreadListMap> threads, uint thread)
    {
        if (!threads.TryGetValue(thread, out var lists))
        {
            lists = new ThreadListMap();

            threads[thread] = lists;
        }

        return lists;
    }

    private sealed class ThreadListMap
    {
        public List<(MemoryAllocation Allocation, TracePosition FreedAt)> Allocations { get; } = [];

        public List<(TracePosition Start, ulong Bytes)> Frees { get; } = [];
    }

    private sealed partial class ThreadMemory
    {
        public ThreadMemory((MemoryAllocation Allocation, TracePosition FreedAt)[] allocations, (TracePosition Start, ulong Bytes)[] frees)
        {
            AllocationStarts = [.. allocations.Select(a => a.Allocation.Start)];
            AllocationBytes = [.. allocations.Select(a => a.Allocation.Bytes)];
            FreedAt = [.. allocations.Select(a => a.FreedAt)];
            AllocationTotals = GetRunningTotals(AllocationBytes);
            FreeStarts = [.. frees.Select(f => f.Start)];
            FreeTotals = GetRunningTotals([.. frees.Select(f => f.Bytes)]);
        }

        private TracePosition[] AllocationStarts { get; }

        private ulong[] AllocationBytes { get; }

        private TracePosition[] FreedAt { get; }

        private ulong[] AllocationTotals { get; }

        private TracePosition[] FreeStarts { get; }

        private ulong[] FreeTotals { get; }

        public (ulong Bytes, int Count) GetAllocatedDuring(TracePosition start, TracePosition end)
        {
            var first = SortedSearch.FirstAtOrAfter(AllocationStarts, start);

            var last = SortedSearch.FirstAtOrAfter(AllocationStarts, end);

            return last > first ? (AllocationTotals[last] - AllocationTotals[first], last - first) : (0, 0);
        }

        public ulong GetFreedDuring(TracePosition start, TracePosition end)
        {
            var first = SortedSearch.FirstAtOrAfter(FreeStarts, start);

            var last = SortedSearch.FirstAtOrAfter(FreeStarts, end);

            return last > first ? FreeTotals[last] - FreeTotals[first] : 0;
        }

        public IEnumerable<(TracePosition Position, ulong Bytes)> GetAllocations() => AllocationStarts.Zip(AllocationBytes);

        public MemoryUsageCurve GetInUseWithin(TracePosition start, TracePosition end)
        {
            var first = SortedSearch.FirstAtOrAfter(AllocationStarts, start);

            var last = SortedSearch.FirstAtOrAfter(AllocationStarts, end);

            if (last <= first)
            {
                return MemoryUsageCurve.Empty;
            }

            var changes = new List<(TracePosition Position, long Bytes)>((last - first) * 2);

            for (var index = first; index < last; index++)
            {
                changes.Add((AllocationStarts[index], (long)AllocationBytes[index]));

                if (FreedAt[index] < end)
                {
                    changes.Add((FreedAt[index], -(long)AllocationBytes[index]));
                }
            }

            return GetCurve(changes);
        }

        public MemoryUsageCurve[] GetInUseOwnedBy(IReadOnlyList<IReadOnlyList<TraceSpan>> owners)
        {
            var ownerIndexes = new int[AllocationStarts.Length];

            Array.Fill(ownerIndexes, -1);

            var ownerStarts = new TracePosition[AllocationStarts.Length];

            for (var owner = 0; owner < owners.Count; owner++)
            {
                foreach (var call in owners[owner])
                {
                    for (var index = SortedSearch.FirstAtOrAfter(AllocationStarts, call.From.Position);
                         index < AllocationStarts.Length && AllocationStarts[index] < call.To.Position;
                         index++)
                    {
                        if (ownerIndexes[index] < 0 || call.From.Position > ownerStarts[index])
                        {
                            ownerIndexes[index] = owner;

                            ownerStarts[index] = call.From.Position;
                        }
                    }
                }
            }

            var changes = new List<(TracePosition Position, long Bytes)>[owners.Count];

            for (var owner = 0; owner < owners.Count; owner++)
            {
                changes[owner] = [];
            }

            for (var index = 0; index < ownerIndexes.Length; index++)
            {
                if (ownerIndexes[index] < 0)
                {
                    continue;
                }

                changes[ownerIndexes[index]].Add((AllocationStarts[index], (long)AllocationBytes[index]));

                if (FreedAt[index].IsKnown)
                {
                    changes[ownerIndexes[index]].Add((FreedAt[index], -(long)AllocationBytes[index]));
                }
            }

            return [.. changes.Select(GetCurve)];
        }

        private static MemoryUsageCurve GetCurve(List<(TracePosition Position, long Bytes)> changes)
        {
            if (changes.Count == 0)
            {
                return MemoryUsageCurve.Empty;
            }

            changes.Sort();

            var positions = new List<TracePosition>(changes.Count);

            var values = new List<ulong>(changes.Count);

            long inUse = 0;

            foreach (var (position, bytes) in changes)
            {
                inUse += bytes;

                if (positions.Count > 0 && positions[^1] == position)
                {
                    values[^1] = (ulong)Math.Max(0, inUse);
                }
                else
                {
                    positions.Add(position);

                    values.Add((ulong)Math.Max(0, inUse));
                }
            }

            return new MemoryUsageCurve([.. positions], [.. values]);
        }

        private static ulong[] GetRunningTotals(ulong[] bytes)
        {
            var totals = new ulong[bytes.Length + 1];

            for (var index = 0; index < bytes.Length; index++)
            {
                totals[index + 1] = totals[index] + bytes[index];
            }

            return totals;
        }
    }
}
