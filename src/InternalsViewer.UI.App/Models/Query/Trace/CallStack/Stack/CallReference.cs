using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Stack;

public sealed record CallReference(CallStackNode Node, int CallIndex, bool IsSelected = false);
