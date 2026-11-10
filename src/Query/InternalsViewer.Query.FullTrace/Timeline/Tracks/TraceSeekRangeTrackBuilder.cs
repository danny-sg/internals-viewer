using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal static class TraceSeekRangeTrackBuilder
{
    private const string Module = "sqlmin";

    private const string ExpressionModule = "sqlTsEs";

    private const string EnumeratorClass = "CRangeEnumeratorMult";

    private const string SetRangeMethod = "ExecSetRange";

    private const string GetDataName = "GetData";

    private const string RangeIteratorClass = "CQScanRangeNew";

    private const string EvaluateRangeMethod = "EvalSetRangeExpr";

    private const string RangeSetMethod = "SetRange";

    private const int RangeArgument = 2;

    private const int KeyArgument = 1;

    public static TraceTickSeries Build(TraceTimeline timeline,
                                        TraceCallStore calls,
                                        TraceOperatorLifetimeList lifetimes,
                                        EnclosingOperatorCallIndex enclosing)
        => Build(timeline, calls, lifetimes, enclosing, IsSetRange, static _ => true);

    public static TraceTickSeries BuildKeys(TraceTimeline timeline,
                                            TraceCallStore calls,
                                            TraceOperatorLifetimeList lifetimes,
                                            EnclosingOperatorCallIndex enclosing)
        => Build(timeline, calls, lifetimes, enclosing, IsGetData, IsRangeKey);

    public static int GetRange(TraceTickSeries ranges,
                               TraceCallStore calls,
                               ExecutionOperatorEvent operatorEvent,
                               uint thread,
                               TracePosition position)
        => ranges.GetTicks(operatorEvent, thread)?.GetLatest(position) is { } latest && GetCall(calls, latest) is { } call
            ? (int)call.GetSlot(RangeArgument)
            : 0;

    public static List<VariantLocator> GetKeys(TraceTickSeries keys,
                                               TraceCallStore calls,
                                               ExecutionOperatorEvent operatorEvent,
                                               uint thread,
                                               TracePosition position)
    {
        if (keys.GetTicks(operatorEvent, thread) is not { } index
            || index.GetLatest(position) is not { } latest
            || GetEvaluationStart(calls, latest) is not { } start)
        {
            return [];
        }

        var found = new List<VariantLocator>();

        for (TraceTick? key = latest;
             key is { } current && current.Span.From.Position >= start;
             key = index.GetPrevious(current.Span.From.Position, static _ => true))
        {
            if (GetCall(calls, current)?.GetLocator(KeyArgument) is not { ValueType: LocatorValueType.Variant } locator)
            {
                return [];
            }

            found.Add(new VariantLocator(locator));
        }

        found.Reverse();

        return found;
    }

    private static TraceTickSeries Build(TraceTimeline timeline,
                                         TraceCallStore calls,
                                         TraceOperatorLifetimeList lifetimes,
                                         EnclosingOperatorCallIndex enclosing,
                                         Func<CallFrame, bool> isFunction,
                                         Func<CallTreeNode, bool> isNode)
    {
        var sources = new List<TraceTickSource>();

        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list)?.Frame is not { } frame || !isFunction(frame))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                if (timeline.FindCall(list.FunctionAddress, list.InstanceAddress, index) is { } location && isNode(location.Node))
                {
                    sources.Add(TraceTickSource.Create(location, 0));
                }
            }
        }

        return TraceTickSeries.Build(timeline, sources, lifetimes, enclosing);
    }

    private static TraceArgumentCall? GetCall(TraceCallStore calls, TraceTick tick)
        => calls.GetCallList(tick.FunctionAddress, tick.InstanceAddress) is { } list && tick.CallIndex < list.Count
            ? list[tick.CallIndex]
            : null;

    private static TracePosition? GetEvaluationStart(TraceCallStore calls, TraceTick key)
    {
        if (GetEvaluationFrame(key.Node) is not { } frame || calls.GetCallList(frame.Address, frame.InstanceAddress) is not { } list)
        {
            return null;
        }

        var index = list.IndexOfEnclosing(key.Span.From.Position, key.Thread);

        return index < 0 ? null : list.GetFromPosition(index);
    }

    private static CallFrame? GetEvaluationFrame(CallTreeNode key) => key.Parent?.Parent?.Frame;

    private static bool IsSetRange(CallFrame frame)
        => frame is { Function: { ClassName: EnumeratorClass, MethodName: SetRangeMethod } }
           && frame.Module.Name.Equals(Module, StringComparison.OrdinalIgnoreCase);

    private static bool IsGetData(CallFrame frame)
        => frame is { Function.MethodName: { } method }
           && method.Contains(GetDataName, StringComparison.Ordinal)
           && frame.Module.Name.Equals(ExpressionModule, StringComparison.OrdinalIgnoreCase);

    private static bool IsRangeKey(CallTreeNode node)
        => GetEvaluationFrame(node) is { Function: { ClassName: RangeIteratorClass, MethodName: EvaluateRangeMethod or RangeSetMethod } };
}
