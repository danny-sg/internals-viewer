using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceOutputRowSource(ExecutionOperatorEvent Operator, IReadOnlyList<int> Columns, bool IsWholeRow);
