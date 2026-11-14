using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Layouts;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class LocatorHook(IEnumerable<LocatorRead> reads, TraceCallStore.Builder store, ImageText? images = null)
    : ArgumentHook<LocatorRead>(reads.Select(r => (r.Function, r)))
{
    private ImageFallbackMemory? Memory { get; set; }

    protected override void Read(ITraceReplay replay, in TraceHookCall call, LocatorRead read, ulong pointer, ReadOnlySpan<ulong> arguments)
    {
        var count = read.CountArgumentSlot < 0 ? 1 : GetArgument(arguments, call, read.CountArgumentSlot);

        if (ReadValue(replay, read, pointer, count) is { } found)
        {
            store.AddLocator(call.FunctionAddress, call.InstanceAddress, (int)call.CallIndex, found, read.IsReadAtReturn);
        }
    }

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

    private Locator? ReadValue(ITraceReplay replay, LocatorRead read, ulong pointer, ulong count)
    {
        if (pointer == 0 || !TryFollow(replay, read.PageSource, ref pointer))
        {
            return null;
        }

        var offset = GetOffset(replay.Layout, read.PageSource);

        return Locator.Read(read.ValueType, GetMemory(replay), read.ArgumentSlot, pointer + offset, count);
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

    private ITraceMemoryReader GetMemory(ITraceReplay replay)
    {
        if (images is null)
        {
            return replay;
        }

        if (Memory is not { } memory || !ReferenceEquals(memory.Replay, replay))
        {
            memory = new ImageFallbackMemory(replay, images);

            Memory = memory;
        }

        return memory;
    }
}
