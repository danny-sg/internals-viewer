using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models;

public sealed record CallValue(CallStackNode Node, int Call)
{
    public override string ToString() => $"{Call + 1}";
}
