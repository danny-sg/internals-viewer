using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

public readonly record struct TraceRegisterChange(TraceRegister Register, ulong Low, ulong High);
