using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal static class TraceSeekRangeReader
{
    private const string Module = "sqlmin";

    private const string EnumeratorClass = "CRangeEnumeratorMult";

    private const string SetRangeMethod = "ExecSetRange";

    private const int RangeArgument = 2;

    public static TraceTickSeries Build(TraceTimeline timeline,
                                        TraceCallStore calls,
                                        TraceOperatorLifetimeList lifetimes,
                                        EnclosingOperatorCallIndex enclosing)
    {
        var sources = new List<TraceTickSource>();

        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list)?.Frame is not { } frame || !IsSetRange(frame))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                if (timeline.FindCall(list.FunctionAddress, list.InstanceAddress, index) is { } location)
                {
                    sources.Add(TraceTickSource.FromCall(location, 0));
                }
            }
        }

        return TraceTickSeries.FromSources(timeline, sources, lifetimes, enclosing);
    }

    public static int GetRange(TraceTickSeries ranges,
                               TraceCallStore calls,
                               ExecutionOperatorEvent operatorEvent,
                               uint thread,
                               TracePosition position)
    {
        if (ranges.GetTicks(operatorEvent, thread)?.GetLatest(position) is not { } latest
            || calls.GetCallList(latest.FunctionAddress, latest.InstanceAddress) is not { } list
            || latest.CallIndex >= list.Count)
        {
            return 0;
        }

        return (int)list[latest.CallIndex].Slot(RangeArgument);
    }

    private static bool IsSetRange(CallFrame frame)
        => frame is { Function: { ClassName: EnumeratorClass, MethodName: SetRangeMethod } }
           && frame.Module.Name.Equals(Module, StringComparison.OrdinalIgnoreCase);
}
