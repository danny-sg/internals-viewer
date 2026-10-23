using System.Collections.Generic;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Tree;

public sealed record CallTreeLocation(EngineEvent? Event, CallStackNode? Node, bool Focus, CallStackNode? Function, int? CallIndex)
{
    public IReadOnlyList<string>? Expanded { get; init; }

    public string? Selected { get; init; }
}
