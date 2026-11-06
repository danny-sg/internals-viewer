using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceCallLink(TracePoint Point, CallTreeNode? Node, int CallIndex);
