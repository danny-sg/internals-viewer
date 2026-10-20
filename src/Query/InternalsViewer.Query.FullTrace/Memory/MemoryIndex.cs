using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryIndex
{
    private MemoryIndex(Dictionary<uint, ThreadMemory> threads,
                        MemoryInUseCurve inUse,
                        MemoryInUseCurve workspace,
                        IReadOnlyList<MemoryBlock>? blocks = null)
    {
        Threads = threads;
        InUse = inUse;
        Workspace = workspace;
        Blocks = blocks ?? [];
    }

    public static MemoryIndex Empty { get; } = new([], MemoryInUseCurve.Empty, MemoryInUseCurve.Empty);

    public bool HasAllocations => Threads.Count > 0;

    public bool HasWorkspace => !Workspace.IsEmpty;

    internal IReadOnlyList<MemoryBlock> Blocks { get; }

    private Dictionary<uint, ThreadMemory> Threads { get; }

    private MemoryInUseCurve InUse { get; }

    private MemoryInUseCurve Workspace { get; }

    internal static MemoryIndex Build(IEnumerable<MemoryAllocation> allocations, IEnumerable<MemoryFree> frees)
    {
        var kept = allocations.GroupBy(a => a.Thread).SelectMany(t => Outermost.Of(t, a => a.Start, a => a.End)).ToList();

        var released = frees.GroupBy(f => f.Thread).SelectMany(t => Outermost.Of(t, f => f.Start, f => f.End)).ToList();

        var events = kept.Select((a, index) => (a.Start, IsFree: false, Index: index))
                         .Concat(released.Select((f, index) => (f.Start, IsFree: true, Index: index)))
                         .OrderBy(e => e.Start)
                         .ThenBy(e => e.IsFree ? 0 : 1);

        var freedAt = kept.Select(a => a.Pointer == 0 ? a.Start : double.PositiveInfinity).ToArray();

        var threads = new Dictionary<uint, ThreadLists>();

        var live = new Dictionary<ulong, int>();

        var positions = new List<double>();

        var values = new List<ulong>();

        var workspacePositions = new List<double>();

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

                if (kept[allocation].Workspace)
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

                if (kept[index].Workspace)
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

        MemoryBlock[] blocks = [.. kept.Select((a, index) => new MemoryBlock(a.Pointer, a.Bytes, a.Thread, a.Start, freedAt[index]))
                                       .Where(b => b.Pointer != 0)];

        return new MemoryIndex(memory,
                               new MemoryInUseCurve([.. positions], [.. values]),
                               new MemoryInUseCurve([.. workspacePositions], [.. workspaceValues]),
                               blocks);
    }

    public (ulong Bytes, int Count) AllocatedDuring(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.AllocatedDuring(start, end) : (0, 0);

    public ulong FreedDuring(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.FreedDuring(start, end) : 0;

    public ulong RetainedBy(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.RetainedBy(start, end) : 0;

    public IEnumerable<(double Position, ulong Bytes)> GetAllocations(uint thread)
        => Threads.TryGetValue(thread, out var memory) ? memory.Allocations() : [];

    public MemoryInUseCurve InUseWithin(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.InUseWithin(start, end) : MemoryInUseCurve.Empty;

    public IReadOnlyList<MemoryInUseCurve> InUseOwnedBy(uint thread, IReadOnlyList<IReadOnlyList<TraceSpan>> owners)
        => Threads.TryGetValue(thread, out var memory)
            ? memory.InUseOwnedBy(owners)
            : [.. owners.Select(_ => MemoryInUseCurve.Empty)];

    public ulong PeakInUseDuring(double start, double end) => InUse.PeakDuring(start, end);

    public ulong PeakWorkspaceDuring(double start, double end) => Workspace.PeakDuring(start, end);

    private static ThreadLists GetLists(Dictionary<uint, ThreadLists> threads, uint thread)
    {
        if (!threads.TryGetValue(thread, out var lists))
        {
            lists = new ThreadLists();

            threads[thread] = lists;
        }

        return lists;
    }

    private sealed class ThreadLists
    {
        public List<(MemoryAllocation Allocation, double FreedAt)> Allocations { get; } = [];

        public List<(double Start, ulong Bytes)> Frees { get; } = [];
    }

    private sealed partial class ThreadMemory
    {
        public ThreadMemory((MemoryAllocation Allocation, double FreedAt)[] allocations, (double Start, ulong Bytes)[] frees)
        {
            AllocationStarts = [.. allocations.Select(a => a.Allocation.Start)];
            AllocationBytes = [.. allocations.Select(a => a.Allocation.Bytes)];
            FreedAt = [.. allocations.Select(a => a.FreedAt)];
            AllocationTotals = GetRunningTotals(AllocationBytes);
            FreeStarts = [.. frees.Select(f => f.Start)];
            FreeTotals = GetRunningTotals([.. frees.Select(f => f.Bytes)]);
        }

        private double[] AllocationStarts { get; }

        private ulong[] AllocationBytes { get; }

        private double[] FreedAt { get; }

        private ulong[] AllocationTotals { get; }

        private double[] FreeStarts { get; }

        private ulong[] FreeTotals { get; }

        public (ulong Bytes, int Count) AllocatedDuring(double start, double end)
        {
            var first = SortedSearch.FirstAtOrAfter(AllocationStarts, start);

            var last = SortedSearch.FirstAtOrAfter(AllocationStarts, end);

            return last > first ? (AllocationTotals[last] - AllocationTotals[first], last - first) : (0, 0);
        }

        public ulong FreedDuring(double start, double end)
        {
            var first = SortedSearch.FirstAtOrAfter(FreeStarts, start);

            var last = SortedSearch.FirstAtOrAfter(FreeStarts, end);

            return last > first ? FreeTotals[last] - FreeTotals[first] : 0;
        }

        public IEnumerable<(double Position, ulong Bytes)> Allocations() => AllocationStarts.Zip(AllocationBytes);

        public MemoryInUseCurve InUseWithin(double start, double end)
        {
            var first = SortedSearch.FirstAtOrAfter(AllocationStarts, start);

            var last = SortedSearch.FirstAtOrAfter(AllocationStarts, end);

            if (last <= first)
            {
                return MemoryInUseCurve.Empty;
            }

            var changes = new List<(double Position, long Bytes)>((last - first) * 2);

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

        public MemoryInUseCurve[] InUseOwnedBy(IReadOnlyList<IReadOnlyList<TraceSpan>> owners)
        {
            var ownerOf = new int[AllocationStarts.Length];

            Array.Fill(ownerOf, -1);

            var ownerStarts = new double[AllocationStarts.Length];

            for (var owner = 0; owner < owners.Count; owner++)
            {
                foreach (var call in owners[owner])
                {
                    for (var index = SortedSearch.FirstAtOrAfter(AllocationStarts, call.Start);
                         index < AllocationStarts.Length && AllocationStarts[index] < call.End;
                         index++)
                    {
                        if (ownerOf[index] < 0 || call.Start > ownerStarts[index])
                        {
                            ownerOf[index] = owner;

                            ownerStarts[index] = call.Start;
                        }
                    }
                }
            }

            var changes = new List<(double Position, long Bytes)>[owners.Count];

            for (var owner = 0; owner < owners.Count; owner++)
            {
                changes[owner] = [];
            }

            for (var index = 0; index < ownerOf.Length; index++)
            {
                if (ownerOf[index] < 0)
                {
                    continue;
                }

                changes[ownerOf[index]].Add((AllocationStarts[index], (long)AllocationBytes[index]));

                if (!double.IsPositiveInfinity(FreedAt[index]))
                {
                    changes[ownerOf[index]].Add((FreedAt[index], -(long)AllocationBytes[index]));
                }
            }

            return [.. changes.Select(GetCurve)];
        }

        public ulong RetainedBy(double start, double end)
        {
            ulong retained = 0;

            for (var index = SortedSearch.FirstAtOrAfter(AllocationStarts, start);
                 index < AllocationStarts.Length && AllocationStarts[index] < end;
                 index++)
            {
                if (FreedAt[index] >= end)
                {
                    retained += AllocationBytes[index];
                }
            }

            return retained;
        }

        private static MemoryInUseCurve GetCurve(List<(double Position, long Bytes)> changes)
        {
            if (changes.Count == 0)
            {
                return MemoryInUseCurve.Empty;
            }

            changes.Sort();

            var positions = new List<double>(changes.Count);

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

            return new MemoryInUseCurve([.. positions], [.. values]);
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
