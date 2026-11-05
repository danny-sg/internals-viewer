using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct MemoryHistoryWriteEntry(TracePosition Position,
                                                        int SpanId,
                                                        int Offset,
                                                        ushort Length,
                                                        MemoryHistoryChangeType Type);
