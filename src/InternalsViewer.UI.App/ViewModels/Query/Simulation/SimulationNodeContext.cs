using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.UI.App.Models.Query.Simulation;

namespace InternalsViewer.UI.App.ViewModels.Query.Simulation;

public sealed class SimulationNodeContext
{
    private static readonly Dictionary<int, SimulationHeldRowsViewModel> EmptyHeldRows = [];
    public required IteratorDefinition Definition { get; init; }

    public required int Depth { get; init; }

    public required Color Colour { get; init; }

    public (int Outer, int Inner) InputNodes { get; init; } = (-1, -1);

    public SimulationOperatorViewModel? Tab { get; init; }

    public SimulationRowStreamViewModel? Stream => Tab?.Output;

    public SimulationVisualViewModel? Visual { get; init; }

    public SimulationVisualViewModel? SourceVisual { get; init; }

    public SimulationHashTableViewModel? HashTable { get; init; }

    public SimulationHashTableViewModel? LocalHashTable { get; init; }

    public SimulationAggregateViewModel? Aggregates { get; init; }

    public SimulationSegmentViewModel? Segment { get; init; }

    public IReadOnlyDictionary<int, SimulationHeldRowsViewModel> HeldRows { get; init; } = EmptyHeldRows;

    public OperatorSides? Sides { get; init; }
}
