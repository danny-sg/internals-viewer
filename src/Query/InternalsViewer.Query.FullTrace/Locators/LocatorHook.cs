using System.Runtime.InteropServices;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Layouts;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class LocatorHook : ITraceHook
{
    public LocatorHook(IEnumerable<LocatorRead> reads, TraceCallStore.Builder store, ImageText? images = null)
    {
        Store = store;

        Images = images;

        var ranges = reads.TryGetNonEnumeratedCount(out var count)
            ? new Dictionary<ulong, (int Start, int Count)>(count)
            : new Dictionary<ulong, (int Start, int Count)>();

        foreach (var read in reads)
        {
            ref var range = ref CollectionsMarshal.GetValueRefOrAddDefault(ranges, read.Function, out _);

            range.Count++;
        }

        var next = 0;

        foreach (var function in ranges.Keys)
        {
            ref var range = ref CollectionsMarshal.GetValueRefOrNullRef(ranges, function);

            var total = range.Count;

            range = (next, 0);

            next += total;
        }

        var all = new LocatorRead[next];

        foreach (var read in reads)
        {
            ref var range = ref CollectionsMarshal.GetValueRefOrNullRef(ranges, read.Function);

            all[range.Start + range.Count++] = read;
        }

        Ranges = ranges;

        Reads = all;
    }

    public IEnumerable<TraceHookFunction> Functions
        => Ranges.Select(r => new TraceHookFunction(r.Key, HasReturnRead(r.Value)
                                                           ? TraceHookEvents.Entry | TraceHookEvents.Return
                                                           : TraceHookEvents.Entry));

    private TraceCallStore.Builder Store { get; }

    private ImageText? Images { get; }

    private Dictionary<ulong, (int Start, int Count)> Ranges { get; }

    private LocatorRead[] Reads { get; }

    private Dictionary<ulong, ulong[]> Starts { get; } = [];

    private Stack<ulong[]> FreeArguments { get; } = [];

    private ImageFallbackMemory? Memory { get; set; }

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Ranges.TryGetValue(call.FunctionAddress, out var range))
        {
            return;
        }

        var functionReads = Reads.AsSpan(range.Start, range.Count);

        var isStackRead = false;

        var isReadAtReturn = false;

        foreach (var read in functionReads)
        {
            isStackRead |= read.ArgumentSlot is >= ArgumentLayout.RegisterCount and < ArgumentLayout.CapturedSlotCount;

            isReadAtReturn |= read.IsReadAtReturn;
        }

        Span<ulong> arguments = stackalloc ulong[ArgumentLayout.CapturedSlotCount];

        call.Arguments.CopyTo(arguments);

        if (isStackRead)
        {
            replay.GetStackArguments(call).CopyTo(arguments[ArgumentLayout.RegisterCount..]);
        }
        else
        {
            arguments[ArgumentLayout.RegisterCount..].Clear();
        }

        foreach (var read in functionReads)
        {
            if (!read.IsReadAtReturn)
            {
                Read(replay, call, read, GetArgument(arguments, call, read.ArgumentSlot), arguments);
            }
        }

        if (isReadAtReturn)
        {
            var saved = FreeArguments.TryPop(out var free) ? free : new ulong[ArgumentLayout.CapturedSlotCount];

            arguments.CopyTo(saved);

            Starts[call.CallId] = saved;
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Starts.Remove(call.CallId, out var arguments))
        {
            return;
        }

        if (Ranges.TryGetValue(call.FunctionAddress, out var range))
        {
            foreach (var read in Reads.AsSpan(range.Start, range.Count))
            {
                if (read.IsReadAtReturn)
                {
                    Read(replay, call, read, GetArgument(arguments, call, read.ArgumentSlot), arguments);
                }
            }
        }

        FreeArguments.Push(arguments);
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        if (Starts.Remove(call.CallId, out var arguments))
        {
            FreeArguments.Push(arguments);
        }
    }

    private static ulong GetArgument(ReadOnlySpan<ulong> arguments, in TraceHookCall call, int argumentSlot)
        => argumentSlot switch
        {
            ArgumentLayout.ReturnArgumentSlot
                => call.ReturnValue,
            >= 0 and < ArgumentLayout.CapturedSlotCount
                => arguments[argumentSlot],
            _ => 0
        };

    private static bool TryFollow(ITraceReplay replay, LocatorPageSource source, ref ulong pointer)
    {
        foreach (var hop in GetPath(replay.Layout, source))
        {
            if (!replay.TryRead(pointer + (uint)hop, out ulong next) || next == 0)
            {
                return false;
            }

            pointer = next;
        }

        return true;
    }

    private static int[] GetPath(EngineLayout layout, LocatorPageSource source) => source switch
    {
        LocatorPageSource.PageHandle => [0, layout.PageLocation.PageHandleBufferField.Offset],
        LocatorPageSource.PageReference => [0],
        _ => []
    };

    private static ulong GetOffset(EngineLayout layout, LocatorPageSource source) => source switch
    {
        LocatorPageSource.AllocationPageId => (ulong)layout.PageLocation.AllocationPageIdField.Offset,
        LocatorPageSource.PageHandle or LocatorPageSource.PageReference => (ulong)layout.Buffer.PageIdField.Offset,
        _ => 0
    };

    private void Read(ITraceReplay replay, in TraceHookCall call, LocatorRead read, ulong pointer, ReadOnlySpan<ulong> arguments)
    {
        var count = read.CountArgumentSlot < 0 ? 1 : GetArgument(arguments, call, read.CountArgumentSlot);

        if (ReadValue(replay, read, pointer, count) is { } found)
        {
            Store.AddLocator(call.FunctionAddress, call.InstanceAddress, (int)call.CallIndex, found, read.IsReadAtReturn);
        }
    }

    private Locator? ReadValue(ITraceReplay replay, LocatorRead read, ulong pointer, ulong count)
    {
        if (pointer == 0 || !TryFollow(replay, read.PageSource, ref pointer))
        {
            return null;
        }

        var offset = GetOffset(replay.Layout, read.PageSource);

        return Locator.Read(read.ValueType, GetMemory(replay), read.ArgumentSlot, pointer + offset, count);
    }

    private ITraceMemoryReader GetMemory(ITraceReplay replay)
    {
        if (Images is null)
        {
            return replay;
        }

        if (Memory is not { } memory || !ReferenceEquals(memory.Replay, replay))
        {
            memory = new ImageFallbackMemory(replay, Images);

            Memory = memory;
        }

        return memory;
    }

    private bool HasReturnRead((int Start, int Count) range)
    {
        foreach (var read in Reads.AsSpan(range.Start, range.Count))
        {
            if (read.IsReadAtReturn)
            {
                return true;
            }
        }

        return false;
    }
}
