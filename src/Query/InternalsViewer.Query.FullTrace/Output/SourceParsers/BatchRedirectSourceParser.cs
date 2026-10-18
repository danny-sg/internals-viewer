using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Sources;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.Output.SourceParsers;

internal static class BatchRedirectSourceParser
{
    private const ulong IndexOffset = 0x08;

    private const ulong HelperOffset = 0x10;

    private const ulong HelperRowOffset = 0x78;

    public static TraceOutputSource? Parse(ITraceReplay replay, ulong value)
    {
        if (!replay.TryRead(value + IndexOffset, out int index) || !replay.TryRead(value + HelperOffset, out ulong helper))
        {
            return null;
        }

        var row = replay.TryRead(helper + HelperRowOffset, out ulong helperRow)
            ? helperRow + (ulong)index * VariantRowParser.ValueSize
            : 0;

        return new BufferOutputSource(value, row) { Owner = helper };
    }
}
