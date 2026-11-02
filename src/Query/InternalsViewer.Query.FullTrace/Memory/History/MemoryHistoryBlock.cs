namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed record MemoryHistoryBlock(ulong Iterator,
                                        ulong Address,
                                        int Size,
                                        bool IsIterator,
                                        bool IsInline,
                                        uint Thread,
                                        int CallNodeIndex,
                                        TracePosition FromPosition,
                                        TracePosition ToPosition,
                                        ulong FromInstructions,
                                        ulong ToInstructions,
                                        int FirstWrite,
                                        int WriteCount)
{
    public bool IsStack { get; init; }

    public int FirstInstance { get; init; }

    public int InstanceCount { get; init; }

    public TracePoint From => new(FromPosition, FromInstructions);

    public TracePoint? To => ToPosition.IsKnown ? new TracePoint(ToPosition, ToInstructions) : null;
}
