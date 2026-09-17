using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed record OperatorRow(ExecutionOperatorEvent Operator, bool Unsegmented, ActivitySpan? Span = null);

/// <summary>
/// The part of the query window an operator was running, as fractions of the window
/// </summary>
public sealed record ActivitySpan(double Start, double End);
