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
    public TracePoint From => new(FromPosition, FromInstructions);

    public TracePoint? To => ToPosition.IsKnown ? new TracePoint(ToPosition, ToInstructions) : null;
}
