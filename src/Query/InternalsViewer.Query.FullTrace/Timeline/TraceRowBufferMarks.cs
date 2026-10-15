using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceRowBufferMarks
{
    private TraceRowBufferMarks(Dictionary<uint, ThreadMarks> threads)
    {
        Threads = threads;

        Count = threads.Values.Sum(t => t.Positions.Length);
    }

    public static TraceRowBufferMarks Empty { get; } = new([]);

    public int Count { get; }

    private Dictionary<uint, ThreadMarks> Threads { get; }

    public ReadOnlySpan<double> GetMarks(uint threadId, TraceTimelineAxis axis)
    {
        if (!Threads.TryGetValue(threadId, out var marks))
        {
            return [];
        }

        return axis == TraceTimelineAxis.Position ? marks.Positions : marks.Instructions;
    }

    internal static TraceRowBufferMarks Build(TraceTimeline timeline, RowBufferIndex rowBuffers)
    {
        if (rowBuffers.Count == 0)
        {
            return Empty;
        }

        var captures = new Dictionary<(uint Thread, ulong Iterator), List<Capture>>();

        foreach (var location in timeline.GetSpanLocations())
        {
            if (location.Call < 0
                || location.Node.Frame is not { } frame
                || !rowBuffers.TryGetIterator(frame.Address, frame.Instance, location.Call, out var iterator))
            {
                continue;
            }

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(captures, (location.Thread.ThreadId, iterator), out _);

            (list ??= []).Add(new Capture(location.StartOf(TraceTimelineAxis.Position),
                                          location.EndOf(TraceTimelineAxis.Position),
                                          location.EndOf(TraceTimelineAxis.Instructions)));
        }

        var threads = new Dictionary<uint, List<Capture>>();

        foreach (var ((thread, _), list) in captures)
        {
            ref var marks = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, thread, out _);

            (marks ??= []).AddRange(GetInnermost(list));
        }

        return new TraceRowBufferMarks(threads.ToDictionary(t => t.Key, t => ThreadMarks.Build(t.Value)));
    }

    private static List<Capture> GetInnermost(List<Capture> captures)
    {
        captures.Sort((left, right) => left.End != right.End ? left.End.CompareTo(right.End) : right.Start.CompareTo(left.Start));

        var innermost = new List<Capture>(captures.Count);

        foreach (var capture in captures)
        {
            if (innermost.Count > 0 && innermost[^1] is var inner && inner.Start >= capture.Start && inner.End <= capture.End)
            {
                continue;
            }

            innermost.Add(capture);
        }

        return innermost;
    }

    private readonly record struct Capture(double Start, double End, double InstructionEnd);

    private sealed record ThreadMarks(double[] Positions, double[] Instructions)
    {
        public static ThreadMarks Build(List<Capture> captures)
        {
            captures.Sort((left, right) => left.End.CompareTo(right.End));

            return new ThreadMarks([.. captures.Select(c => c.End)], [.. captures.Select(c => c.InstructionEnd)]);
        }
    }
}
