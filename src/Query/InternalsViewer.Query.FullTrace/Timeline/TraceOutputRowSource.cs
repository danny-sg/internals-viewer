using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceOutputRowSource(ExecutionOperatorEvent Operator,
                                          TraceRowBufferMark? RowBuffer,
                                          IReadOnlyList<TraceOutputColumn> Columns);
