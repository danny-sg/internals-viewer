using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Tree;

public sealed record CallTreeLocation(EngineEvent? Event, CallTreeNode? Node, bool Focus, CallTreeNode? Function, int? CallIndex)
{
    public IReadOnlyList<string>? Expanded { get; init; }

    public string? Selected { get; init; }
}
