using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Sources;

namespace InternalsViewer.Query.FullTrace.Output.SourceParsers;

internal static class TableSourceParser
{
    private const ulong RowsetOffset = 0x08;

    private const ulong SlotOffset = 0x10;

    private const ulong AccessorOffset = 0x18;

    public static TraceOutputSource? Parse(ITraceReplay replay, ulong value, ulong context)
    {
        if (!replay.TryRead(value + RowsetOffset, out ulong rowset) || !replay.TryRead(value + SlotOffset, out int slot))
        {
            return null;
        }

        var accessor = replay.TryRead(value + AccessorOffset, out ulong handle) ? handle : 0;

        return new RowsetOutputSource(value, rowset) { Slot = slot, Accessor = accessor, Context = context };
    }
}
