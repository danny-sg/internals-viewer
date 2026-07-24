using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed record OperatorRow(ExecutionOperatorEvent Operator, bool Unsegmented);
