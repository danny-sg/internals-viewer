using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Memory.History;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct MemoryHistoryReadEntry(TracePosition Position, int SpanId, int Offset, int Length, int Count);
