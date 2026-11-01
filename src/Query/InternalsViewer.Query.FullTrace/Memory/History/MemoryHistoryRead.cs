using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Memory.History;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct MemoryHistoryRead(TracePosition Position,
                                                 uint LowInstructions,
                                                 int CallNodeIndex,
                                                 int CallIndex,
                                                 int Offset,
                                                 int Length,
                                                 int Count);
