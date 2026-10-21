namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryWrite(int Block,
                                                  TracePosition Position,
                                                  ulong Instructions,
                                                  ulong ProgramCounter,
                                                  uint Thread,
                                                  int CallNodeIndex,
                                                  int CallIndex,
                                                  ulong Iterator,
                                                  int FirstChange,
                                                  int ChangeCount)
{
    public TracePoint Point => new(Position, Instructions);
}
