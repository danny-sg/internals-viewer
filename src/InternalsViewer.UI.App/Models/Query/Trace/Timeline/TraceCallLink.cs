using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceCallLink(TracePoint Point, CallStackNode? Node, int CallIndex);
