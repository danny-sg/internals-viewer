using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public readonly record struct MemoryRequest(ulong Address, int Length, TracePosition? Position = null);
