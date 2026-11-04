namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

public readonly record struct TraceInstructionStep(TracePosition Position,
                                                  ulong ProgramCounter,
                                                  int FirstChange,
                                                  int ChangeCount,
                                                  int Depth);
