using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct MemoryHistoryAccessEntry(TracePosition Position,
                                                         int SpanId,
                                                         int Offset,
                                                         int Length,
                                                         int Count,
                                                         MemoryHistoryAccessType Type)
{
    public bool HasData => Type != MemoryHistoryAccessType.Read;
}
