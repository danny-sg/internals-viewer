using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public static class TraceReads
{
    private const string Module = "sqlmin";

    private const string BufferPool = "BPool";

    private const string GetMethod = "Get";

    private const string ReadAheadMethod = "ReadAhead";

    private const int PageCountSlot = 3;

    public static (PageAddress Page, int PageCount)? GetRead(TraceArgumentCall call, TraceReadType type)
        => GetPage(call) is { } page ? (page, GetPageCount(call, type)) : null;

    internal static TraceTickSeries Build(TraceTimeline timeline,
                                          TraceCallStore calls,
                                          TraceOperatorLifetimes lifetimes,
                                          EnclosingOperatorCalls? enclosing = null)
        => TraceTickSeries.FromCalls(timeline,
                                     calls,
                                     lifetimes,
                                     f => GetReadType(f) is not null,
                                     (f, c) => GetReadType(f) is { } type && GetPage(c) is not null ? (byte)type : null,
                                     enclosing ?? EnclosingOperatorCalls.Build(calls, lifetimes.OperatorsByInstance));

    internal static bool IsBufferPool(CallstackFrame? frame)
        => frame is { Resolved.ClassName: BufferPool } && frame.Module.Equals(Module, StringComparison.OrdinalIgnoreCase);

    private static TraceReadType? GetReadType(CallstackFrame frame)
        => IsBufferPool(frame) && frame.Resolved is { } resolved
            ? resolved.MethodName switch
            {
                GetMethod => TraceReadType.Page,
                ReadAheadMethod => TraceReadType.ReadAhead,
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
