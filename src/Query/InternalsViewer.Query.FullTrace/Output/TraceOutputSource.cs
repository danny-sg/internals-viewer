using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Output;

public sealed record TraceOutputSource(OutputSourceType Type, ulong Value, ulong Address)
{
    public static TraceOutputSource Unknown { get; } = new(OutputSourceType.Unknown, 0, 0);

    public int Slot { get; init; } = -1;

    public int Ordinal { get; init; } = -1;

    public int Offset { get; init; }

    public int Length { get; init; }

    public ulong Owner { get; init; }

    internal ulong State { get; init; }
}
