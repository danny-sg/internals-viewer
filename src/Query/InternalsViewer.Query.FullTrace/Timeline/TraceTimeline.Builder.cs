using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    internal sealed class Builder
    {
        private Dictionary<uint, ThreadSpanList> Threads { get; } = [];

        public unsafe void Add(IntPtr spans, int count) => Add(new ReadOnlySpan<CallSpan>((void*)spans, count));

        public void Add(ReadOnlySpan<CallSpan> spans)
        {
            foreach (var span in spans)
            {
                if (!Threads.TryGetValue(span.Thread, out var thread))
                {
                    thread = new ThreadSpanList();

                    Threads[span.Thread] = thread;
                }

                thread.Add(span);
            }
        }

        public TraceTimeline Build(CallNode[] nodes)
        {
            var depths = new int[nodes.Length];

            var parents = new int[nodes.Length];

            for (var index = 0; index < nodes.Length; index++)
            {
                var parent = nodes[index].ParentIndex;

                parents[index] = parent >= 0 && parent < index ? parent : -1;

                depths[index] = parents[index] >= 0 ? depths[parent] + 1 : 0;
            }

            var scale = new TraceScale(Threads.Values.Select(t => t.MaximumSteps).DefaultIfEmpty(0ul).Max() + 1d);

            var threads = Threads.Select(t => t.Value.Build(t.Key, depths, scale))
                                 .Where(t => t.CallCount > 0)
                                 .OrderBy(t => t.Start.Position)
                                 .ToList();

            var timeline = new TraceTimeline(threads, parents, scale);

            timeline.SetCuts();

            return timeline;
        }
    }

    private sealed class ThreadSpanList
    {
        private const int ExcludedNode = -2;

        private const int ChunkShift = 11;

        private const int ChunkSize = 1 << ChunkShift;

        private const int ChunkMask = ChunkSize - 1;

        public ulong MaximumSteps { get; private set; }

        private List<PendingSpan[]?> Chunks { get; } = [];

        private TraceThreadClock.Builder Clock { get; } = new();

        private int Count { get; set; }

        private List<(TracePosition From, TracePosition To)> Excluded { get; } = [];

        public void Add(CallSpan span)
        {
            var from = new TracePosition(span.StartSequence, span.StartSteps);

            var to = new TracePosition(span.EndSequence, span.EndSteps);

            Clock.Add(from, span.StartInstructions);

            Clock.Add(to, span.EndInstructions);

            MaximumSteps = Math.Max(MaximumSteps, Math.Max(span.StartSteps, span.EndSteps));

            if (span.CallNodeIndex == ExcludedNode)
            {
                Excluded.Add((from, to));

                return;
            }

            if ((Count & ChunkMask) == 0)
            {
                Chunks.Add(new PendingSpan[ChunkSize]);
            }

            Chunks[^1]![Count & ChunkMask] = new PendingSpan(from, to, span.CallNodeIndex, span.CallIndex, (byte)span.Flags);

            Count++;
        }

        public TraceTimelineThread Build(uint threadId, int[] depths, TraceScale scale)
        {
            var count = Count;

            var rowCounts = new List<int>();

            for (var index = 0; index < count; index++)
            {
                var depth = GetDepth(Chunks[index >> ChunkShift]![index & ChunkMask], depths);

                while (rowCounts.Count <= depth)
                {
                    rowCounts.Add(0);
                }

                rowCounts[depth]++;
            }

            var starts = new int[rowCounts.Count];

            for (var depth = 1; depth < starts.Length; depth++)
            {
                starts[depth] = starts[depth - 1] + rowCounts[depth - 1];
            }

            var next = (int[])starts.Clone();

            var store = new TraceSpanStore(count);

            for (var chunk = 0; chunk < Chunks.Count; chunk++)
            {
                var spans = Chunks[chunk]!;

                var filled = Math.Min(ChunkSize, count - (chunk << ChunkShift));

                for (var offset = 0; offset < filled; offset++)
                {
                    var span = spans[offset];

                    store.Set(next[GetDepth(span, depths)]++, span.From, span.To, span.CallNodeIndex, span.CallIndex, span.Flags);
                }

                Chunks[chunk] = null;
            }

            Chunks.Clear();

            Count = 0;

            var clock = Clock.Build();

            var rows = new TraceTimelineRow[starts.Length];

            for (var depth = 0; depth < rows.Length; depth++)
            {
                store.Sort(starts[depth], rowCounts[depth]);

                rows[depth] = new TraceTimelineRow(scale, clock, store, starts[depth], rowCounts[depth]);
            }

            var thread = new TraceTimelineThread(threadId, clock, store, rows, count);

            clock.AddDeadSpans(thread.GetMaterialDeadSpans(Excluded, _ => false, scale));

            Excluded.Clear();

            return thread;
        }

        private static int GetDepth(in PendingSpan span, int[] depths)
            => span.CallNodeIndex >= 0 && span.CallNodeIndex < depths.Length ? depths[span.CallNodeIndex] : 0;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private readonly record struct PendingSpan(TracePosition From, TracePosition To, int CallNodeIndex, uint CallIndex, byte Flags);
    }
}
