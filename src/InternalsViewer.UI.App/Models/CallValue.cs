using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models;

public sealed record CallValue(CallStackNode Node, int CallIndex)
{
    public override string ToString() => $"{CallIndex + 1}";
}
