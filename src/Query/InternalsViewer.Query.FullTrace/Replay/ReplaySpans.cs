using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Replay.Native;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Replay;

internal sealed class ReplaySpans
{
    internal IReadOnlyDictionary<uint, ThreadSpans> Threads => ThreadLists;

    private Dictionary<uint, ThreadSpans> ThreadLists { get; } = [];

    public unsafe void Add(IntPtr spans, int count) => Add(new ReadOnlySpan<CallSpan>((void*)spans, count));

    public void Add(ReadOnlySpan<CallSpan> spans)
    {
        foreach (var span in spans)
        {
            if (!ThreadLists.TryGetValue(span.Thread, out var thread))
            {
                thread = new ThreadSpans();

                ThreadLists[span.Thread] = thread;
            }

            thread.Add(span);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly record struct PackedSpan(TracePosition From, TracePosition To, int ReplayNodeIndex, uint CallIndex, byte Flags);

    internal sealed class ThreadSpans
    {
        private const int ExcludedNode = -2;

        private const int ChunkShift = 11;

        private const int ChunkSize = 1 << ChunkShift;

        private const int ChunkMask = ChunkSize - 1;

        public ulong MaximumSteps { get; private set; }

        public int Count { get; private set; }

        public int ChunkCount => Chunks.Count;

        public TraceThreadClock.Builder Clock { get; } = new();

        public List<(TracePosition From, TracePosition To)> Excluded { get; } = [];

        private List<PackedSpan[]?> Chunks { get; } = [];

        public void Add(CallSpan span)
        {
            var from = new TracePosition(span.FromSequence, span.FromSteps);

            var to = new TracePosition(span.ToSequence, span.ToSteps);

            Clock.Add(from, span.FromInstructions);

            Clock.Add(to, span.ToInstructions);

            MaximumSteps = Math.Max(MaximumSteps, Math.Max(span.FromSteps, span.ToSteps));

            if (span.ReplayNodeIndex == ExcludedNode)
            {
                Excluded.Add((from, to));

                return;
            }

            if ((Count & ChunkMask) == 0)
            {
                Chunks.Add(new PackedSpan[ChunkSize]);
            }

            Chunks[^1]![Count & ChunkMask] = new PackedSpan(from, to, span.ReplayNodeIndex, span.CallIndex, (byte)span.Flags);

            Count++;
        }

        public ReadOnlySpan<PackedSpan> GetChunk(int chunk) => Chunks[chunk].AsSpan(0, GetFilled(chunk));

        public ReadOnlySpan<PackedSpan> TakeChunk(int chunk)
        {
            var spans = Chunks[chunk];

            Chunks[chunk] = null;

            return spans.AsSpan(0, GetFilled(chunk));
        }

        private int GetFilled(int chunk) => Math.Min(ChunkSize, Count - (chunk << ChunkShift));
    }
}
