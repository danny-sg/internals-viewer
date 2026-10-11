using System;

namespace InternalsViewer.UI.App.Models.Query.Simulation;

public enum SimulationPaneKind
{
    Empty,
    Visual,
    RowStream,
    HeldRows,
    HashTable,
    HashTables,
    Aggregates,
    Segment
}

/// <summary>
/// One pane of an operator's tab, which is what it shows and what that view binds to
/// </summary>
/// <remarks>
/// The definition tree decides the kind, so an input that is another operator resolves to that operator's results rather than to a visual.
/// The panes are laid out by one view rather than by the dock, because an operator is a single document and its panes are not moveable.
/// </remarks>
public sealed record SimulationPane(SimulationPaneKind Kind, object? Content, string Title = "")
{
    public static readonly SimulationPane Empty = new(SimulationPaneKind.Empty, null);

    public int? SourceNodeId { get; init; }

    public Windows.UI.Color? AccentColour { get; init; }

    public Uri? Icon { get; init; }

    public string Heading { get; init; } = "";

    public string Subheading { get; init; } = "";
}
