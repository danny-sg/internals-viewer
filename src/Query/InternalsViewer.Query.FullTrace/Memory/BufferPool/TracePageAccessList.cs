using System.Runtime.CompilerServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public sealed class TracePageAccessList
{
    private const int ThisRegister = 0;

    private TracePageAccessList(TracePageAccess[] accesses)
    {
        Accesses = accesses;
    }

    public static TracePageAccessList Empty { get; } = new([]);

    public int Count => Accesses.Length;

    public long Bytes => Accesses.Length * (long)Unsafe.SizeOf<TracePageAccess>();

    private TracePageAccess[] Accesses { get; }

    public TracePageAccess this[int index] => Accesses[index];

    public static TracePageAccessList Build(TraceCallStore calls, MemoryHistoryStore history, TraceBufferPool pool)
    {
        var accesses = new List<TracePageAccess>();

        AddFetches(calls, pool, accesses);

        AddRecordCalls(calls, pool, accesses);

        AddWrittenPointers(history, pool, accesses);

        var ordered = new List<TracePageAccess>(accesses.Count);

        foreach (var access in accesses.OrderBy(a => a.Position))
        {
            if (ordered.Count > 0 && IsRepeat(ordered[^1], access))
            {
                continue;
            }

            ordered.Add(access);
        }

        return new TracePageAccessList([.. ordered]);
    }

    private static void AddFetches(TraceCallStore calls, TraceBufferPool pool, List<TracePageAccess> accesses)
    {
        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list)?.Frame is not { Resolved.MethodName: BufferPoolFunctionLookup.GetMethod } frame
                || !BufferPoolFunctionLookup.IsBufferPool(frame))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                var buffer = call.Returned ? pool.GetReference(call.ReturnValue, call.ToPosition) : null;

                if ((GetPage(call) ?? buffer?.Page) is { } page)
                {
                    accesses.Add(new TracePageAccess(call.FromPosition, page, buffer?.DatabaseId ?? 0, null));
                }
            }
        }
    }

    private static void AddRecordCalls(TraceCallStore calls, TraceBufferPool pool, List<TracePageAccess> accesses)
    {
        var buffer = new TraceCallStore.ColumnBuffer();

        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list)?.Frame is not { Resolved.ClassName.Length: > 0 } frame
                || !frame.Module.Equals(BufferPoolFunctionLookup.Module, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var (columns, _) in list.ReadChunks(buffer))
            {
                for (var callIndex = 0; callIndex < columns.Calls; callIndex++)
                {
                    var position = columns.GetFromPosition(callIndex);

                    if (GetRecord(pool, columns.GetRegister(callIndex, ThisRegister), position) is { } access)
                    {
                        accesses.Add(access);
                    }
                }
            }
        }
    }

    private static void AddWrittenPointers(MemoryHistoryStore history, TraceBufferPool pool, List<TracePageAccess> accesses)
    {
        for (var block = 0; block < history.Blocks.Count; block++)
        {
            foreach (var (position, value) in MemoryHistoryPointerReader.GetWrittenPointers(history, block))
            {
                if (GetRecord(pool, value, position) is { } access)
                {
                    accesses.Add(access);
                }
            }
        }
    }

    private static TracePageAccess? GetRecord(TraceBufferPool pool, ulong address, TracePosition position)
        => address != 0 && pool.GetFrameReference(address, position) is { } reference && reference.Offset >= PageHeader.Size
            ? new TracePageAccess(position, reference.Page, reference.DatabaseId, reference.Offset)
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

    private static bool IsRepeat(TracePageAccess previous, TracePageAccess access)
        => previous.Page == access.Page && previous.DatabaseId == access.DatabaseId && previous.Offset == access.Offset;
}
