using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class LocatorHook(IEnumerable<LocatorRead> reads, TraceCallStore.Builder store, ImageText? images = null)
    : ArgumentHook<LocatorRead>(reads.Select(r => (r.Function, r)))
{
    private ImageFallbackMemory? Memory { get; set; }

    protected override void Read(ITraceReplay replay, in TraceHookCall call, LocatorRead read, ulong pointer, ReadOnlySpan<ulong> arguments)
    {
        var count = read.CountRegister < 0 ? 1 : GetArgument(arguments, call, read.CountRegister);

        var locator = read.ValueType == LocatorValueType.Argument
            ? new Locator(read.Register, read.ValueType, pointer)
            : ReadValue(replay, read, pointer, count);

        if (locator is { } found)
        {
            store.AddLocator(call.Function, call.Instance, (int)call.CallIndex, found, read.IsReadAtReturn);
        }
    }

    private static bool TryFollow(ITraceReplay replay, int[]? path, ref ulong pointer)
    {
        foreach (var hop in path ?? [])
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
        if (pointer == 0 || !TryFollow(replay, read.Path, ref pointer))
        {
            return null;
        }

        return Locator.Read(read.ValueType, GetMemory(replay), read.Register, pointer + (uint)read.Offset, count);
    }

    private IMemoryReader GetMemory(ITraceReplay replay)
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
