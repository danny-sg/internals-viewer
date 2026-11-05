using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public static class TraceReadResolver
{
    private const int PageCountSlot = 3;

    public static (PageAddress Page, int PageCount)? GetRead(TraceArgumentCall call, TraceReadType type)
        => GetPage(call) is { } page ? (page, GetPageCount(call, type)) : null;

    internal static TraceTickSeries Build(TraceTimeline timeline,
                                          TraceCallStore calls,
                                          TraceOperatorLifetimeList lifetimes,
                                          EnclosingOperatorCallIndex? enclosing = null)
        => TraceTickSeries.FromCalls(timeline,
                                     calls,
                                     lifetimes,
                                     f => GetReadType(f) is not null,
                                     (f, c) => GetReadType(f) is { } type && GetPage(c) is not null ? (byte)type : null,
                                     enclosing ?? EnclosingOperatorCallIndex.Build(calls, lifetimes.OperatorsByInstance));

    private static TraceReadType? GetReadType(CallstackFrame frame)
        => BufferPoolFunctionLookup.IsBufferPool(frame) && frame.Function is { } resolved
            ? resolved.MethodName switch
            {
                BufferPoolFunctionLookup.GetMethod => TraceReadType.Page,
                BufferPoolFunctionLookup.ReadAheadMethod => TraceReadType.ReadAhead,
                _ => null
            }
            : null;

    private static int GetPageCount(TraceArgumentCall call, TraceReadType type)
        => type == TraceReadType.ReadAhead && call.IntegerSlots.Length > PageCountSlot ? (int)call.IntegerSlots[PageCountSlot] : 1;

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
