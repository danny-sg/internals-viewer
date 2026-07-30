using System.Collections.Generic;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;

namespace InternalsViewer.UI.App.Models;

public sealed record PlanNodeProperty(string Name, string Value)
{
    public List<PlanNodeProperty> Children { get; init; } = [];

    public PredicateText? Predicate { get; init; }

    public bool IsExpanded { get; init; } = true;

    public List<string> Items { get; init; } = [];

    public bool IsNameMonospace { get; init; }

    public bool IsValueMonospace { get; init; }

    public bool IsValueHighlighted { get; init; }

    public bool IsValueSuccess { get; init; }

    public bool IsValueError { get; init; }

    public string? Tooltip { get; init; }

    public int Depth { get; init; }
}
