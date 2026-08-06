using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Services.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class TraceTabViewModelFactory(IIteratorFactory iteratorFactory, IndexService indexService)
{
    /// <summary>
    /// Builds a trace of an operator and everything below it, or null when some operator in that tree cannot be simulated
    /// </summary>
    public TraceTabViewModel? Create(DatabaseSource database,
                                     PlanNode node,
                                     Func<PlanNode, AllocationUnit?> resolveUnit,
                                     DateTime? queryTime,
                                     ScanModeResult? scanMode,
                                     bool wrapInSelect = false)
    {
        var builder = new TraceDefinitionBuilder(resolveUnit, database);

        if (builder.Build(node) is not { } built)
        {
            return null;
        }

        var definition = wrapInSelect
            ? new SelectDefinition(built) { NodeId = -1, OutputList = built.OutputList }
            : built;

        var visuals = TraceSourceCollector.Collect(definition)
                                          .Select(s => CreateVisual(database, s, builder))
                                          .OfType<TraceVisualViewModel>()
                                          .ToList();

        if (visuals.Count == 0)
        {
            return null;
        }

        var layout = TraceLayoutBuilder.Build(definition,
                                              visuals.ToDictionary(v => v.NodeId),
                                              id => builder.Nodes.GetValueOrDefault(id));

        return new TraceTabViewModel(iteratorFactory, definition, database, node, queryTime, scanMode, visuals, layout);
    }

    private TraceVisualViewModel? CreateVisual(DatabaseSource database, TraceSource source, TraceDefinitionBuilder builder)
    {
        if (!builder.Units.TryGetValue(source.NodeId, out var unit))
        {
            return null;
        }

        var visualType = source.VisualType == TraceSourceKind.Index ? TraceVisualType.Index : TraceVisualType.Allocation;

        var title = source.Role == TraceSourceRole.None
            ? $"{DisplayName(unit)} ({source.NodeId})"
            : $"{source.Role}: {DisplayName(unit)} ({source.NodeId})";

        return new TraceVisualViewModel(visualType, database, unit, indexService, title, source.NodeId)
        {
            ShowObjectBorderImmediately = source.VisualType == TraceSourceKind.Heap
        };
    }

    private static string DisplayName(AllocationUnit unit)
        => string.IsNullOrEmpty(unit.IndexName) ? unit.TableName : unit.IndexName;
}
