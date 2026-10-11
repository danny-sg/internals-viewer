using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Simulation;

namespace InternalsViewer.UI.App.ViewModels.Query.Simulation;

public sealed class SimulationLayout
{
    public required IReadOnlyList<SimulationOperatorViewModel> Tabs { get; init; }

    public required IReadOnlyDictionary<int, SimulationNodeContext> Nodes { get; init; }

    public required SimulationBlobPalette Palette { get; init; }
}
