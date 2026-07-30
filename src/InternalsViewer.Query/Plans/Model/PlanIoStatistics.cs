namespace InternalsViewer.Query.Parsing.Plans;

public sealed record PlanIoStatistics
{
    public long LogicalReads { get; init; }

    public long PhysicalReads { get; init; }

    public long ReadAheads { get; init; }

    public long Scans { get; init; }

    public long Rebinds { get; init; }

    public long Rewinds { get; init; }

    public long LobLogicalReads { get; init; }

    public long LobPhysicalReads { get; init; }

    public long LobReadAheads { get; init; }
}
