using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct MemoryHistoryWrite(TracePosition Position,
                                                  uint LowInstructions,
                                                  int CallNodeIndex,
                                                  int CallIndex,
                                                  int Offset,
                                                  int DataIndex,
                                                  ushort Length,
                                                  MemoryHistoryChangeType Type);
