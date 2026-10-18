using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Sources;

namespace InternalsViewer.Query.FullTrace.Output.SourceParsers;

internal static class HashSourceParser
{
    private const ulong OffsetOffset = 0x10;

    private const ulong OrdinalOffset = 0x14;

    private const ulong LengthOffset = 0x2A;

    private const ulong SlotOffset = 0x2C;

    public static TraceOutputSource? Parse(ITraceReplay replay, ulong value, ulong context)
    {
        if (!replay.TryRead(value + SlotOffset, out int slot)
            || !replay.TryRead(value + OffsetOffset, out int offset)
            || !replay.TryRead(value + OrdinalOffset, out ushort ordinal)
            || !replay.TryRead(value + LengthOffset, out ushort length))
        {
            return null;
        }

        return new HashRecordOutputSource(value)
        {
            Slot = slot,
            Offset = offset,
            Ordinal = ordinal,
            Length = length,
            Context = context
        };
    }
}
