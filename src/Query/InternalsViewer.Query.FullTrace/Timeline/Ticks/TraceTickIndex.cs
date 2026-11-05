using System.Runtime.CompilerServices;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed class TraceTickIndex
{
    internal TraceTickIndex(TraceTimeline timeline,
                            TraceTimelineThread thread,
                            ExecutionOperatorEvent operatorEvent,
                            List<TraceTickSource> sources)
    {
        sources.Sort(static (left, right) => left.From.Position.CompareTo(right.From.Position));

        Timeline = timeline;

        Thread = thread;

        Operator = operatorEvent;

        Scale = timeline.Scale;

        Entries = [.. sources.Select(s => new TraceTickEntry(s.SpanIndex, s.Value))];

        Froms = [.. sources.Select(s => s.From)];

        Tos = [.. sources.Select(s => s.To)];

        Callers = sources.Any(s => s.Caller is not null) ? [.. sources.Select(s => s.Caller)] : null;

        RunningEnds = new TracePoint[Tos.Length];

        for (var index = 0; index < Tos.Length; index++)
        {
            RunningEnds[index] = index > 0 && RunningEnds[index - 1].Position > Tos[index].Position ? RunningEnds[index - 1] : Tos[index];
        }
    }

    public ExecutionOperatorEvent Operator { get; }

    public int Count => Entries.Length;

    public TraceScale Scale { get; }

    public IReadOnlyList<TraceTick> All => [.. Enumerable.Range(0, Count).Select(Get)];

    internal long Bytes => Count * (Unsafe.SizeOf<TraceTickEntry>() + 3L * Unsafe.SizeOf<TracePoint>())
                           + (Callers is null ? 0 : Count * (long)IntPtr.Size);

    private TraceTimeline Timeline { get; }

    private TraceTimelineThread Thread { get; }

    private TraceTickEntry[] Entries { get; }

    private TracePoint[] Froms { get; }

    private TracePoint[] Tos { get; }

    private TracePoint[] RunningEnds { get; }

    private ExecutionOperatorEvent?[]? Callers { get; }

    public TraceTick Get(int index)
    {
        var entry = Entries[index];

        var store = Thread.Store;

        var callNodeIndex = store.CallNodeIndexes[entry.SpanIndex];

        var span = new TraceSpan(Froms[index],
                                 Tos[index],
                                 callNodeIndex,
                                 store.GetCallIndex(entry.SpanIndex),
                                 (TraceSpanFlags)store.Flags[entry.SpanIndex]);

        return new TraceTick(entry.Value, Thread.ThreadId, Timeline.NodeOf(callNodeIndex)!, span, Operator, Callers?[index]);
    }

    public byte GetValue(int index) => Entries[index].Value;

    public double GetStart(TraceTimelineAxis axis, int index) => Scale.GetValue(Froms[index], axis);

    public double GetEnd(TraceTimelineAxis axis, int index) => Scale.GetValue(Tos[index], axis);

    public int GetFirstEndingFrom(TraceTimelineAxis axis, double value) => Scale.GetFirstAtOrAfter(RunningEnds, axis, value);

    public int GetFirstStartingAfter(TraceTimelineAxis axis, double value) => Scale.GetFirstAfter(Froms, axis, value);

    public TraceTick? GetNearest(TraceTimelineAxis axis, double value, double tolerance)
    {
        var nearest = -1;

        var best = double.MaxValue;

        for (var index = Scale.GetFirstAfter(Froms, axis, value + tolerance) - 1;
             index >= 0 && Scale.GetValue(RunningEnds[index], axis) >= value - tolerance;
             index--)
        {
            var start = GetStart(axis, index);

            var end = GetEnd(axis, index);

            var distance = value < start ? start - value : value > end ? value - end : 0;

            if (distance <= tolerance
                && (nearest < 0 || IsInside(index, nearest) || (!IsInside(nearest, index) && distance < best)))
            {
                nearest = index;

                best = distance;
            }
        }

        return nearest >= 0 ? Get(nearest) : null;
    }

    private bool IsInside(int inner, int outer)
        => Froms[outer].Position <= Froms[inner].Position
           && Tos[inner].Position <= Tos[outer].Position
           && (Froms[inner].Position != Froms[outer].Position || Tos[inner].Position != Tos[outer].Position);
}
