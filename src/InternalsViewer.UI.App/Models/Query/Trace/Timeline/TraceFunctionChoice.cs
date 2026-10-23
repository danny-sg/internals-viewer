using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceFunctionChoice(string Module, string Symbol, CallstackFrame Frame)
{
    public string Label => $"{Module}!{Symbol}";

    public override string ToString() => Label;
}
