using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Sources;

namespace InternalsViewer.Query.FullTrace.Output.SourceParsers;

internal static class ConstantSourceParser
{
    private const ulong VariantOffset = 0x08;

    public static TraceOutputSource? Parse(ITraceReplay replay, ulong value)
        => replay.TryRead(value + VariantOffset, out ulong variant)
            ? new BufferOutputSource(value, variant)
            : null;
}
