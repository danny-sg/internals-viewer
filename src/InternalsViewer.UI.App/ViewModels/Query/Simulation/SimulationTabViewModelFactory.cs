using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Services.Query.Simulation;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Query.Simulation;

public sealed class SimulationTabViewModelFactory(ILogger<SimulationTabViewModel> logger,
                                             IIteratorFactory iteratorFactory,
                                             IndexService indexService)
{
    /// <summary>
    /// Builds a trace of an operator and everything below it, or null when some operator in that tree cannot be simulated
    /// </summary>
    public SimulationTabViewModel? Create(DatabaseSource database,
                                     PlanNode node,
                                     Func<PlanNode, AllocationUnit?> resolveUnit,
                                     DateTime? queryTime,
                                     ScanModeResult? scanMode,
                                     bool wrapInSelect = false)
    {
        var builder = new SimulationDefinitionBuilder(resolveUnit, database);

        if (builder.Build(node) is not { } built)
        {
            return null;
        }

        var definition = wrapInSelect || built is BatchToRowDefinition
            ? new SelectDefinition(built) { NodeId = -1, OutputList = built.OutputList }
            : built;

        var visuals = SimulationSourceCollector.Collect(definition)
                                          .Select(s => CreateVisual(database, s, builder))
                                          .OfType<SimulationVisualViewModel>()
                                          .ToList();

        if (visuals.Count == 0)
        {
            return null;
        }

        var layout = SimulationLayoutBuilder.Build(definition,
                                              visuals.ToDictionary(v => v.NodeId),
                                              id => builder.Nodes.GetValueOrDefault(id));

        return new SimulationTabViewModel(logger, iteratorFactory, definition, database, node, queryTime, scanMode, visuals, layout);
    }

    private SimulationVisualViewModel? CreateVisual(DatabaseSource database, SimulationSource source, SimulationDefinitionBuilder builder)
    {
        if (!builder.Units.TryGetValue(source.NodeId, out var unit))
        {
            return null;
        }

        var visualType = source.VisualType switch
        {
            SimulationSourceKind.Index => SimulationVisualType.Index,
            SimulationSourceKind.Columnstore => SimulationVisualType.Columnstore,
            _ => SimulationVisualType.Allocation
        };

        var title = source.Role == SimulationSourceRole.None
            ? $"{unit.DisplayName()} ({source.NodeId})"
            : $"{source.Role}: {unit.DisplayName()} ({source.NodeId})";

        return new SimulationVisualViewModel(visualType, database, unit, indexService, title, source.NodeId)
        {
            ShowObjectBorderImmediately = source.VisualType == SimulationSourceKind.Heap
        };
    }
}
