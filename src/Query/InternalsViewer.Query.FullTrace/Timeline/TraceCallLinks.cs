using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceCallLinks(ExecutionOperatorEvent Caller, double[] Starts, double[] Ends);
