using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceFunctionChoice(string Module, string Symbol, CallFrame Frame)
{
    public string Label => $"{Module}!{Symbol}";

    public override string ToString() => Label;
}
