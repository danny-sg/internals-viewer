namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryInstance(TracePosition FromPosition,
                                                    TracePosition ToPosition,
                                                    ulong FromInstructions,
                                                    ulong ToInstructions)
{
    public TracePoint From => new(FromPosition, FromInstructions);

    public TracePoint? To => ToPosition.IsKnown ? new TracePoint(ToPosition, ToInstructions) : null;
}
