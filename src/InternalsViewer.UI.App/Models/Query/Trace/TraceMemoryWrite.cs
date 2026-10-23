using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.Trace;

public readonly record struct TraceMemoryWrite(TracePoint Point, int Offset, int Length, int CallNodeIndex);
