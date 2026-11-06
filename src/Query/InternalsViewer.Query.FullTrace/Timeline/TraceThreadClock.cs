namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceThreadClock
{
    private TraceThreadClock(uint[] sequences, ulong[] starts)
    {
        Sequences = sequences;
        Starts = starts;
    }

    public int Count => Sequences.Length;

    internal long Bytes => Sequences.Length * (long)sizeof(uint)
                           + Starts.Length * (long)sizeof(ulong)
                           + DeadFroms.Length * 3L * sizeof(ulong);

    private uint[] Sequences { get; }

    private ulong[] Starts { get; }

    private TracePosition[] DeadFroms { get; set; } = [];

    private TracePosition[] DeadTos { get; set; } = [];

    private ulong[] DeadBefore { get; set; } = [];

    public IEnumerable<(TracePosition From, TracePosition To)> GetDeadSpans() => DeadFroms.Select((f, i) => (f, DeadTos[i]));

    public ulong GetInstructions(TracePosition position)
    {
        var instructions = GetRecordedInstructions(position);

        if (DeadFroms.Length == 0 || !position.IsKnown)
        {
            return instructions;
        }

        var index = Array.BinarySearch(DeadFroms, position);

        index = index >= 0 ? index : ~index - 1;

        if (index < 0)
        {
            return instructions;
        }

        return position < DeadTos[index]
            ? Subtract(GetRecordedInstructions(DeadFroms[index]), DeadBefore[index])
            : Subtract(instructions, DeadBefore[index] + GetDeadInstructions(index));
    }

    internal ulong GetRecordedInstructions(TracePosition position)
    {
        if (!position.IsKnown)
        {
            return ulong.MaxValue;
        }

        var index = Array.BinarySearch(Sequences, (uint)position.Sequence);

        if (index >= 0)
        {
            return Starts[index] + position.Steps;
        }

        var previous = ~index - 1;

        return previous >= 0 ? Starts[previous] : 0;
    }

    internal void AddDeadSpans(IEnumerable<(TracePosition From, TracePosition To)> spans)
    {
        var merged = new List<(TracePosition From, TracePosition To)>();

        foreach (var (from, to) in GetDeadSpans().Concat(spans).OrderBy(s => s.From))
        {
            if (merged.Count > 0 && from <= merged[^1].To)
            {
                merged[^1] = (merged[^1].From, to > merged[^1].To ? to : merged[^1].To);
            }
            else
            {
                merged.Add((from, to));
            }
        }

        DeadFroms = [.. merged.Select(s => s.From)];

        DeadTos = [.. merged.Select(s => s.To)];

        DeadBefore = new ulong[merged.Count];

        ulong removed = 0;

        for (var index = 0; index < merged.Count; index++)
        {
            DeadBefore[index] = removed;

            removed += GetDeadInstructions(index);
        }
    }

    private static ulong Subtract(ulong left, ulong right) => left > right ? left - right : 0;

    private ulong GetDeadInstructions(int index)
        => Subtract(GetRecordedInstructions(DeadTos[index]), GetRecordedInstructions(DeadFroms[index]));

    internal sealed class Builder
    {
        private Dictionary<uint, ulong> Starts { get; } = [];

        public void Add(TracePosition position, ulong instructions)
        {
            if (position.IsKnown)
            {
                Starts.TryAdd((uint)position.Sequence, instructions - position.Steps);
            }
        }

        public TraceThreadClock Build()
        {
            var sequences = Starts.Keys.ToArray();

            Array.Sort(sequences);

            var starts = new ulong[sequences.Length];

            for (var index = 0; index < sequences.Length; index++)
            {
                starts[index] = Starts[sequences[index]];
            }

            return new TraceThreadClock(sequences, starts);
        }
    }
}
