using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Stack;

public sealed record CallReference(CallTreeNode Node, int CallIndex, bool IsSelected = false);
