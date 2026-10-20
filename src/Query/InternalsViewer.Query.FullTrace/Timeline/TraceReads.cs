using System.Runtime.InteropServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceReads
{
    private const string Module = "sqlmin";

    private const string BufferPool = "BPool";

    private const string GetMethod = "Get";

    private const string ReadAheadMethod = "ReadAhead";

    private const int PageCountSlot = 3;

    private TraceReads(TraceRead[] reads)
    {
        Reads = reads;

        ByOperator = OperatorMarks<TraceRead>.Build(reads, r => r.Operator, r => r.Thread);
    }

    public static TraceReads Empty { get; } = new([]);

    public int Count => Reads.Length;

    public IReadOnlyList<TraceRead> All => Reads;

    private TraceRead[] Reads { get; }

    private OperatorMarks<TraceRead> ByOperator { get; }

    public TraceMarkIndex<TraceRead>? GetReads(ExecutionOperatorEvent operatorEvent, uint thread) => ByOperator.Get(operatorEvent, thread);

    internal static TraceReads Build(TraceTimeline timeline, TraceCallStore calls)
    {
        var reads = new List<TraceRead>();

        var froms = new List<TracePosition>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (calls.GetCallList(address, instance) is not { Count: > 0 } list
                || GetReadType(calls.GetNode(list.GetNodeIndex(0))?.Frame) is not { } type)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (GetPage(call) is not { } page || timeline.FindCall(address, instance, index) is not { } location)
                {
                    continue;
                }

                var pageCount = type == TraceReadType.ReadAhead && call.IntegerSlots.Length > PageCountSlot
                    ? (int)call.IntegerSlots[PageCountSlot]
                    : 1;

                reads.Add(new TraceRead(type,
                                        page,
                                        pageCount,
                                        call.ThreadId,
                                        location.Node,
                                        location.Call,
                                        location.Span(TraceTimelineAxis.Position),
                                        location.Span(TraceTimelineAxis.Instructions)));

                froms.Add(call.FromPosition);
            }
        }

        var operators = OperatorMarks<TraceRead>.GetEnclosing(calls,
                                                              timeline.GetOperatorsByInstance(),
                                                              [.. reads.Select((r, i) => (r.Thread, froms[i]))]);

        return new TraceReads([.. reads.Select((r, i) => operators[i] is { } found ? r with { Operator = found } : r)]);
    }

    private static TraceReadType? GetReadType(CallstackFrame? frame)
        => frame is { Resolved: { ClassName: BufferPool } resolved } && frame.Module.Equals(Module, StringComparison.OrdinalIgnoreCase)
            ? resolved.MethodName switch
            {
                GetMethod => TraceReadType.Page,
                ReadAheadMethod => TraceReadType.ReadAhead,
                _ => null
            }
            : null;

    private static PageAddress? GetPage(TraceArgumentCall call)
    {
        foreach (var locator in call.Locators)
        {
            if (locator.ValueType == LocatorValueType.Page)
            {
                return locator.Page;
            }
        }

        return null;
    }
}
