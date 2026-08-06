using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class TraceNodeContext
{
    public required IteratorDefinition Definition { get; init; }

    public required int Depth { get; init; }

    public required Color Colour { get; init; }

    public (int Outer, int Inner) InputNodes { get; init; } = (-1, -1);

    public TraceOperatorViewModel? Tab { get; init; }

    public TraceRowStreamViewModel? Stream => Tab?.Output;

    public TraceVisualViewModel? Visual { get; init; }

    public TraceVisualViewModel? SourceVisual { get; init; }

    public TraceHashTableViewModel? HashTable { get; init; }

    public IReadOnlyDictionary<int, TraceHeldRowsViewModel> HeldRows { get; init; } = EmptyHeldRows;

    public OperatorSides? Sides { get; init; }

    private static readonly Dictionary<int, TraceHeldRowsViewModel> EmptyHeldRows = [];
}
