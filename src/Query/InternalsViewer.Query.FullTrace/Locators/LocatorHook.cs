using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Locators.Parsers;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class LocatorHook(IEnumerable<LocatorRead> reads, TraceCallStore.Builder store, ImageText? images = null)
    : ArgumentHook<LocatorRead>(reads.Select(r => (r.Function, r)))
{
    private LocatorParsers Parsers { get; } = new(images);

    protected override void Read(ITraceReplay replay, in TraceHookCall call, LocatorRead read, ulong pointer)
    {
        var locator = read.ValueType == LocatorValueType.Argument
            ? new Locator(read.Register, read.ValueType, pointer)
            : ReadValue(replay, read, pointer);

        if (locator is { } found)
        {
            store.AddLocator(call.Function, call.Instance, (int)call.Call, found, read.IsReadAtReturn);
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

    private Locator? ReadValue(ITraceReplay replay, LocatorRead read, ulong pointer)
    {
        if (pointer == 0 || !TryFollow(replay, read.Path, ref pointer) || Parsers.Get(read.ValueType) is not { } parser)
        {
            return null;
        }

        return parser.Read(replay, read.Register, pointer + (uint)read.Offset);
    }
}
