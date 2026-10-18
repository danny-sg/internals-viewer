using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.Output.Sources;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Output;

public abstract partial record TraceOutputSource(ulong Value)
{
    public static TraceOutputSource Unknown { get; } = new UnknownOutputSource();

    public int Slot { get; init; } = -1;

    public ulong Owner { get; init; }

    internal ulong Context { get; init; }

    internal abstract OutputSourceType Type { get; }

    internal abstract RowBufferColumn? GetColumn(RowBuffer row);
}
