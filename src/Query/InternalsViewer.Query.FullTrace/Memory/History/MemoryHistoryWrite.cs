namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryWrite(TracePosition Position,
                                                  ulong Instructions,
                                                  int CallNodeIndex,
                                                  int CallIndex,
                                                  int FirstChange,
                                                  int ChangeCount)
{
    public TracePoint Point => new(Position, Instructions);
}
