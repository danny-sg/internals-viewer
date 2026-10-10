using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed record CallReference(CallStackNode Node, int Call);
