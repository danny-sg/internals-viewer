namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceThreadClock
{
    private TraceThreadClock(uint[] sequences, ulong[] starts)
    {
        Sequences = sequences;
        Starts = starts;
    }

    public static TraceThreadClock Empty { get; } = new([], []);

    public int Count => Sequences.Length;

    internal long Bytes => Sequences.Length * (long)sizeof(uint) + Starts.Length * (long)sizeof(ulong);

    private uint[] Sequences { get; }

    private ulong[] Starts { get; }

    public ulong GetInstructions(TracePosition position)
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
