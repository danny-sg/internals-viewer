using System.Runtime.InteropServices;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

internal sealed class EnclosingOperatorCalls
{
    private EnclosingOperatorCalls(Dictionary<uint, Entry[]> threads)
    {
        Threads = threads;
    }

    private Dictionary<uint, Entry[]> Threads { get; }

    public static EnclosingOperatorCalls Build(TraceCallStore calls, IReadOnlyDictionary<ulong, ExecutionOperatorEvent> owners)
    {
        var threads = new Dictionary<uint, List<Entry>>();

        var buffer = new TraceCallStore.ColumnBuffer();

        foreach (var (functionAddress, instanceAddress) in calls.FunctionKeys)
        {
            if (!owners.ContainsKey(instanceAddress) || calls.GetCallList(functionAddress, instanceAddress) is not { Count: > 0 } list)
            {
                continue;
            }

            var isRowReturn = IteratorSymbols.IsRowReturnMethod(calls.GetNode(list.GetCallNodeIndex(0))?.Frame?.Resolved?.MethodName);

            foreach (var (columns, _) in list.ReadChunks(buffer))
            {
                for (var callIndex = 0; callIndex < columns.Calls; callIndex++)
                {
                    ref var entries = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, columns.GetThread(callIndex), out _);

                    (entries ??= []).Add(new Entry(columns.GetFromPosition(callIndex),
                                                   columns.GetToPosition(callIndex),
                                                   instanceAddress,
                                                   isRowReturn));
                }
            }
        }

        return new EnclosingOperatorCalls(threads.ToDictionary(t => t.Key, t => Sort(t.Value)));
    }

    public ulong[] GetEnclosing(IReadOnlyList<SweepSpan<TracePosition>> points, bool includeRowReturns)
    {
        var enclosing = new ulong[points.Count];

        var byThread = new Dictionary<uint, List<int>>();

        for (var index = 0; index < points.Count; index++)
        {
            ref var indexes = ref CollectionsMarshal.GetValueRefOrAddDefault(byThread, points[index].Thread, out _);

            (indexes ??= []).Add(index);
        }

        var open = new Stack<(TracePosition To, ulong InstanceAddress)>();

        foreach (var (thread, indexes) in byThread)
        {
            indexes.Sort((left, right) => Compare(points[left].From, points[left].To, points[right].From, points[right].To));

            var spans = Threads.GetValueOrDefault(thread) ?? [];

            var next = 0;

            open.Clear();

            foreach (var pointIndex in indexes)
            {
                var point = points[pointIndex];

                for (; next < spans.Length && Compare(spans[next].From, spans[next].To, point.From, point.To) < 0; next++)
                {
                    if (includeRowReturns || !spans[next].IsRowReturn)
                    {
                        Close(open, spans[next].From);

                        open.Push((spans[next].To, spans[next].InstanceAddress));
                    }
                }

                Close(open, point.From);

                enclosing[pointIndex] = open.TryPeek(out var inner) ? inner.InstanceAddress : 0;

                open.Push((point.To, point.InstanceAddress));
            }
        }

        return enclosing;
    }

    private static Entry[] Sort(List<Entry> entries)
    {
        var sorted = entries.ToArray();

        Array.Sort(sorted, static (left, right) => Compare(left.From, left.To, right.From, right.To));

        return sorted;
    }

    private static int Compare(TracePosition leftFrom, TracePosition leftTo, TracePosition rightFrom, TracePosition rightTo)
        => leftFrom != rightFrom ? leftFrom.CompareTo(rightFrom) : rightTo.CompareTo(leftTo);

    private static void Close(Stack<(TracePosition To, ulong InstanceAddress)> open, TracePosition position)
    {
        while (open.TryPeek(out var outer) && outer.To <= position)
        {
            open.Pop();
        }
    }

    private readonly record struct Entry(TracePosition From, TracePosition To, ulong InstanceAddress, bool IsRowReturn);
}
