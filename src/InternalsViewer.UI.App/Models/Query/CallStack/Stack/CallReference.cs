using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Stack;

public sealed record CallReference(CallStackNode Node, int CallIndex, bool IsSelected = false);
