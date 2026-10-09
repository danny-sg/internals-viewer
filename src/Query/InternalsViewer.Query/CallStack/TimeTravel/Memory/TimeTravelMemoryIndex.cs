using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

internal sealed class TimeTravelMemoryIndex
{
    private TimeTravelMemoryIndex(Dictionary<uint, ThreadMemory> threads, TimeTravelInUseCurve inUse)
    {
        Threads = threads;
        InUse = inUse;
    }

    public static TimeTravelMemoryIndex Empty { get; } = new([], TimeTravelInUseCurve.Empty);

    public bool HasAllocations => Threads.Count > 0;

    private Dictionary<uint, ThreadMemory> Threads { get; }

    private TimeTravelInUseCurve InUse { get; }

    public static TimeTravelMemoryIndex Build(IEnumerable<TimeTravelAllocation> allocations, IEnumerable<TimeTravelFree> frees)
    {
        var kept = Outermost(allocations, a => a.Thread, a => a.Start, a => a.End);

        var released = Outermost(frees, f => f.Thread, f => f.Start, f => f.End);

        var events = kept.Select((a, index) => (a.Start, IsFree: false, Index: index))
                         .Concat(released.Select((f, index) => (f.Start, IsFree: true, Index: index)))
                         .OrderBy(e => e.Start)
                         .ThenBy(e => e.IsFree ? 0 : 1);

        var freedAt = kept.Select(a => a.Pointer == 0 ? a.Start : double.PositiveInfinity).ToArray();

        var threads = new Dictionary<uint, ThreadLists>();

        var live = new Dictionary<ulong, int>();

        var positions = new List<double>();

        var values = new List<ulong>();

        ulong inUse = 0;

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

                ListsOf(threads, free.Thread).Frees.Add((start, kept[allocation].Bytes));

                inUse -= Math.Min(inUse, kept[allocation].Bytes);
            }
            else
            {
                if (kept[index].Pointer == 0)
                {
                    continue;
                }

                live[kept[index].Pointer] = index;

                inUse += kept[index].Bytes;
            }

            positions.Add(start);

            values.Add(inUse);
        }

        for (var index = 0; index < kept.Count; index++)
        {
            ListsOf(threads, kept[index].Thread).Allocations.Add((kept[index], freedAt[index]));
        }

        var memory = threads.ToDictionary(t => t.Key, t => new ThreadMemory([.. t.Value.Allocations], [.. t.Value.Frees]));

        return new TimeTravelMemoryIndex(memory, new TimeTravelInUseCurve([.. positions], [.. values]));
    }

    public (ulong Bytes, int Count) AllocatedDuring(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.AllocatedDuring(start, end) : (0, 0);

    public ulong FreedDuring(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.FreedDuring(start, end) : 0;

    public ulong RetainedBy(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.RetainedBy(start, end) : 0;

    public IEnumerable<(double Position, ulong Bytes)> AllocationsOf(uint thread)
        => Threads.TryGetValue(thread, out var memory) ? memory.Allocations() : [];

    public TimeTravelInUseCurve InUseWithin(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.InUseWithin(start, end) : TimeTravelInUseCurve.Empty;

    public IReadOnlyList<TimeTravelInUseCurve> InUseOwnedBy(uint thread, IReadOnlyList<IReadOnlyList<TimeTravelTimelineSpan>> owners)
        => Threads.TryGetValue(thread, out var memory)
            ? memory.InUseOwnedBy(owners)
            : [.. owners.Select(_ => TimeTravelInUseCurve.Empty)];

    public ulong InUseAt(double position) => InUse.ValueAt(position);

    public ulong PeakInUseDuring(double start, double end) => InUse.PeakDuring(start, end);

    private static List<T> Outermost<T>(IEnumerable<T> items, Func<T, uint> threadOf, Func<T, double> startOf, Func<T, double> endOf)
    {
        var kept = new List<T>();

        foreach (var thread in items.GroupBy(threadOf))
        {
            var end = double.MinValue;

            foreach (var item in thread.OrderBy(startOf))
            {
                if (startOf(item) < end)
                {
                    continue;
                }

                kept.Add(item);

                end = endOf(item);
            }
        }

        return kept;
    }

    private static ThreadLists ListsOf(Dictionary<uint, ThreadLists> threads, uint thread)
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
        public List<(TimeTravelAllocation Allocation, double FreedAt)> Allocations { get; } = [];

        public List<(double Start, ulong Bytes)> Frees { get; } = [];
    }

    private sealed class ThreadMemory
    {
        public ThreadMemory((TimeTravelAllocation Allocation, double FreedAt)[] allocations, (double Start, ulong Bytes)[] frees)
        {
            AllocationStarts = [.. allocations.Select(a => a.Allocation.Start)];
            AllocationBytes = [.. allocations.Select(a => a.Allocation.Bytes)];
            FreedAt = [.. allocations.Select(a => a.FreedAt)];
            AllocationTotals = Totals(AllocationBytes);
            FreeStarts = [.. frees.Select(f => f.Start)];
            FreeTotals = Totals([.. frees.Select(f => f.Bytes)]);
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

        public TimeTravelInUseCurve InUseWithin(double start, double end)
        {
            var first = SortedSearch.FirstAtOrAfter(AllocationStarts, start);

            var last = SortedSearch.FirstAtOrAfter(AllocationStarts, end);

            if (last <= first)
            {
                return TimeTravelInUseCurve.Empty;
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

            return CurveOf(changes);
        }

        public TimeTravelInUseCurve[] InUseOwnedBy(IReadOnlyList<IReadOnlyList<TimeTravelTimelineSpan>> owners)
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

            return [.. changes.Select(CurveOf)];
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

        private static TimeTravelInUseCurve CurveOf(List<(double Position, long Bytes)> changes)
        {
            if (changes.Count == 0)
            {
                return TimeTravelInUseCurve.Empty;
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

            return new TimeTravelInUseCurve([.. positions], [.. values]);
        }

        private static ulong[] Totals(ulong[] bytes)
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
