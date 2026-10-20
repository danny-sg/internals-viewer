using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.RowSources;

public sealed record TraceOutputRowSource(ExecutionOperatorEvent Operator, IReadOnlyList<int> Columns, bool IsWholeRow);
