using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

internal sealed class EnclosingOperatorCallIndex
{
    private EnclosingOperatorCallIndex(SweepSpan<TracePosition>[] spans, bool[] isRowReturn)
    {
        Spans = spans;

        IsRowReturn = isRowReturn;
    }

    private SweepSpan<TracePosition>[] Spans { get; }

    private bool[] IsRowReturn { get; }

    public static EnclosingOperatorCallIndex Build(TraceCallStore calls, IReadOnlyDictionary<ulong, ExecutionOperatorEvent> owners)
    {
        var spans = new List<(SweepSpan<TracePosition> Span, bool IsRowReturn)>();

        var buffer = new TraceCallStore.ColumnBuffer();

        foreach (var list in calls.GetCallLists())
        {
            if (!owners.ContainsKey(list.InstanceAddress))
            {
                continue;
            }

            var isRowReturn = IteratorSymbolLookup.IsRowReturnMethod(calls.GetFirstNode(list)?.Frame?.Function?.MethodName);

            foreach (var (columns, _) in list.ReadChunks(buffer))
            {
                for (var callIndex = 0; callIndex < columns.CallCount; callIndex++)
                {
                    spans.Add((new SweepSpan<TracePosition>(columns.GetThread(callIndex),
                                                            columns.GetFromPosition(callIndex),
                                                            columns.GetToPosition(callIndex),
                                                            list.InstanceAddress),
                               isRowReturn));
                }
            }
        }

        spans.Sort(static (left, right) => EnclosingSweep.Compare(left.Span, right.Span));

        return new([.. spans.Select(s => s.Span)], [.. spans.Select(s => s.IsRowReturn)]);
    }

    public ulong[] GetEnclosing(IReadOnlyList<SweepSpan<TracePosition>> points, bool includeRowReturns)
        => EnclosingSweep.GetEnclosing<TracePosition>(Spans, [.. points], includeRowReturns ? default : IsRowReturn);
}
