using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Output;

public sealed record TraceOutputRowSource(ExecutionOperatorEvent Operator, IReadOnlyList<int> Columns, bool IsWholeRow);
