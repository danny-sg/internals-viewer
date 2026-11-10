using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public static class TraceReadResolver
{
    private const int PageCountSlot = 3;

    public static (PageAddress Page, int PageCount, bool IsReadAhead)? GetRead(CallFrame frame, TraceArgumentCall call)
        => GetReadType(frame) is { } type && GetPage(call) is { } page
            ? (page, GetPageCount(call, type), type == TraceReadType.ReadAhead)
            : null;

    private static TraceReadType? GetReadType(CallFrame frame)
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
