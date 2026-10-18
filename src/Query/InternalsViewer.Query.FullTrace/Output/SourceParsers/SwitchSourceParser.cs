using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Output.SourceParsers;

internal static class SwitchSourceParser
{
    private const ulong PrimaryOffset = 0x10;

    private const ulong CachedInnerOffset = 0x08;

    public static ulong? GetInner(ITraceReplay replay, ulong value, OutputValueClass valueClass)
    {
        var offset = valueClass switch
        {
            OutputValueClass.HashSwitch => PrimaryOffset,
            OutputValueClass.HashCachedSwitch => CachedInnerOffset,
            _ => 0UL
        };

        return offset != 0 && replay.TryRead(value + offset, out ulong inner) ? inner : null;
    }
}
