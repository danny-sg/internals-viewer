using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Replay;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    internal static TraceTimeline Build(ReplaySpans spans, TraceNodeMap nodes)
    {
        var depths = new int[nodes.Count];

        for (var index = 0; index < depths.Length; index++)
        {
            var parent = nodes.GetParent(index);

            depths[index] = parent >= 0 ? depths[parent] + 1 : 0;
        }

        var scale = new TraceScale(spans.Threads.Values.Select(t => t.MaximumSteps).DefaultIfEmpty(0ul).Max() + 1d);

        var threads = spans.Threads.Select(t => BuildThread(t.Key, t.Value, depths, scale))
                           .Where(t => t.CallCount > 0)
                           .OrderBy(t => t.Start.Position)
                           .ToList();

        var timeline = new TraceTimeline(threads, nodes, scale);

        timeline.SetCuts();

        return timeline;
    }

    private static TraceTimelineThread BuildThread(uint threadId, ReplaySpans.ThreadSpans spans, int[] depths, TraceScale scale)
    {
        var count = spans.Count;

        var rowCounts = new List<int>();

        for (var chunk = 0; chunk < spans.ChunkCount; chunk++)
        {
            foreach (var span in spans.GetChunk(chunk))
            {
                var depth = GetDepth(span, depths);

                while (rowCounts.Count <= depth)
                {
                    rowCounts.Add(0);
                }

                rowCounts[depth]++;
            }
        }

        var starts = new int[rowCounts.Count];

        for (var depth = 1; depth < starts.Length; depth++)
        {
            starts[depth] = starts[depth - 1] + rowCounts[depth - 1];
        }

        var next = (int[])starts.Clone();

        var store = new TraceSpanStore(count);

        for (var chunk = 0; chunk < spans.ChunkCount; chunk++)
        {
            foreach (var span in spans.TakeChunk(chunk))
            {
                store.Set(next[GetDepth(span, depths)]++, span.From, span.To, span.ReplayNodeIndex, span.CallIndex, span.Flags);
            }
        }

        var clock = spans.Clock.Build();

        var rows = new TraceTimelineRow[starts.Length];

        for (var depth = 0; depth < rows.Length; depth++)
        {
            store.Sort(starts[depth], rowCounts[depth]);

            rows[depth] = new TraceTimelineRow(scale, clock, store, starts[depth], rowCounts[depth]);
        }

        var thread = new TraceTimelineThread(threadId, clock, store, rows, count);

        clock.AddDeadSpans(thread.GetMaterialDeadSpans(spans.Excluded, scale));

        return thread;
    }

    private static int GetDepth(in ReplaySpans.PackedSpan span, int[] depths)
        => span.ReplayNodeIndex >= 0 && span.ReplayNodeIndex < depths.Length ? depths[span.ReplayNodeIndex] : 0;
}
