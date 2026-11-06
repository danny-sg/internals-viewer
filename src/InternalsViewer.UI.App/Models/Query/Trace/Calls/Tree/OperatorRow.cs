using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Tree;

public sealed record OperatorRow(ExecutionOperatorEvent Operator, bool Unsegmented);
