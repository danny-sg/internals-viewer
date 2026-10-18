using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Sources;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.Output.SourceParsers;

internal static class VariantTableSourceParser
{
    private const ulong SlotOffset = 0x08;

    private const ulong OrdinalOffset = 0x0C;

    public static TraceOutputSource? Parse(ITraceReplay replay, ulong value, ulong context)
    {
        if (!replay.TryRead(value + SlotOffset, out int slot) || !replay.TryRead(value + OrdinalOffset, out int ordinal))
        {
            return null;
        }

        var row = replay.TryRead(context + (ulong)slot * sizeof(ulong), out ulong variants)
            ? variants + (ulong)ordinal * VariantRowParser.ValueSize
            : 0;

        return new BufferOutputSource(value, row) { Slot = slot, Context = context };
    }
}
