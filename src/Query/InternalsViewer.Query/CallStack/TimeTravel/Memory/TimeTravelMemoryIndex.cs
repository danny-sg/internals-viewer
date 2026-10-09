namespace InternalsViewer.Query.CallStack.TimeTravel.Memory;

internal sealed class TimeTravelMemoryIndex
{
    private TimeTravelMemoryIndex(Dictionary<uint, ThreadMemory> threads, double[] curvePositions, ulong[] curveValues)
    {
        Threads = threads;
        CurvePositions = curvePositions;
        CurveValues = curveValues;
    }

    public static TimeTravelMemoryIndex Empty { get; } = new([], [], []);

    public bool HasAllocations => Threads.Count > 0;

    private Dictionary<uint, ThreadMemory> Threads { get; }

    private double[] CurvePositions { get; }

    private ulong[] CurveValues { get; }

    public static TimeTravelMemoryIndex Build(IEnumerable<TimeTravelAllocation> allocations, IEnumerable<TimeTravelFree> frees)
    {
        var kept = Outermost(allocations, a => a.Thread, a => a.Start, a => a.End);

        var released = Outermost(frees, f => f.Thread, f => f.Start, f => f.End);

        var events = kept.Select((a, index) => (a.Start, IsFree: false, Index: index))
                         .Concat(released.Select((f, index) => (f.Start, IsFree: true, Index: index)))
                         .OrderBy(e => e.Start)
                         .ThenBy(e => e.IsFree ? 0 : 1);

        var freedAt = Enumerable.Repeat(double.PositiveInfinity, kept.Count).ToArray();

        var matched = new List<(uint Thread, double Start, ulong Bytes)>();

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

                matched.Add((free.Thread, start, kept[allocation].Bytes));

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

        var threads = new Dictionary<uint, ThreadMemory>();

        foreach (var group in kept.Select((a, index) => (Allocation: a, FreedAt: freedAt[index])).GroupBy(a => a.Allocation.Thread))
        {
            threads[group.Key] = new ThreadMemory([.. group.OrderBy(a => a.Allocation.Start)],
                                                  [.. matched.Where(m => m.Thread == group.Key).Select(m => (m.Start, m.Bytes))]);
        }

        foreach (var group in matched.Where(m => !threads.ContainsKey(m.Thread)).GroupBy(m => m.Thread))
        {
            threads[group.Key] = new ThreadMemory([], [.. group.Select(m => (m.Start, m.Bytes))]);
        }

        return new TimeTravelMemoryIndex(threads, [.. positions], [.. values]);
    }

    public (ulong Bytes, int Count) AllocatedDuring(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.AllocatedDuring(start, end) : (0, 0);

    public ulong FreedDuring(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.FreedDuring(start, end) : 0;

    public ulong RetainedBy(uint thread, double start, double end)
        => Threads.TryGetValue(thread, out var memory) ? memory.RetainedBy(start, end) : 0;

    public IEnumerable<(double Position, ulong Bytes)> AllocationsOf(uint thread)
        => Threads.TryGetValue(thread, out var memory) ? memory.Allocations() : [];

    public ulong InUseAt(double position)
    {
        var index = FirstFrom(CurvePositions, position, inclusive: true) - 1;

        return index >= 0 ? CurveValues[index] : 0;
    }

    public ulong PeakInUseDuring(double start, double end)
    {
        var peak = InUseAt(start);

        for (var index = FirstFrom(CurvePositions, start, inclusive: true);
             index < CurvePositions.Length && CurvePositions[index] < end;
             index++)
        {
            peak = Math.Max(peak, CurveValues[index]);
        }

        return peak;
    }

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

    private static int FirstFrom(double[] values, double value, bool inclusive)
    {
        var low = 0;

        var high = values.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (values[middle] < value || (inclusive && values[middle] == value))
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private sealed class ThreadMemory
    {
        public ThreadMemory((TimeTravelAllocation Allocation, double FreedAt)[] allocations, (double Start, ulong Bytes)[] frees)
        {
            AllocationStarts = [.. allocations.Select(a => a.Allocation.Start)];
            AllocationBytes = [.. allocations.Select(a => a.Allocation.Bytes)];
            FreedAt = [.. allocations.Select(a => a.FreedAt)];
            AllocationTotals = Totals(AllocationBytes);

            var ordered = frees.OrderBy(f => f.Start).ToArray();

            FreeStarts = [.. ordered.Select(f => f.Start)];
            FreeTotals = Totals([.. ordered.Select(f => f.Bytes)]);
        }

        private double[] AllocationStarts { get; }

        private ulong[] AllocationBytes { get; }

        private double[] FreedAt { get; }

        private ulong[] AllocationTotals { get; }

        private double[] FreeStarts { get; }

        private ulong[] FreeTotals { get; }

        public (ulong Bytes, int Count) AllocatedDuring(double start, double end)
        {
            var first = FirstFrom(AllocationStarts, start, inclusive: false);

            var last = FirstFrom(AllocationStarts, end, inclusive: false);

            return last > first ? (AllocationTotals[last] - AllocationTotals[first], last - first) : (0, 0);
        }

        public ulong FreedDuring(double start, double end)
        {
            var first = FirstFrom(FreeStarts, start, inclusive: false);

            var last = FirstFrom(FreeStarts, end, inclusive: false);

            return last > first ? FreeTotals[last] - FreeTotals[first] : 0;
        }

        public IEnumerable<(double Position, ulong Bytes)> Allocations() => AllocationStarts.Zip(AllocationBytes);

        public ulong RetainedBy(double start, double end)
        {
            ulong retained = 0;

            for (var index = FirstFrom(AllocationStarts, start, inclusive: false);
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
