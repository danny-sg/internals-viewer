using InternalsViewer.Execution.AccessPaths.Definitions;

namespace InternalsViewer.UI.App.Services.Query.Simulation;

/// <summary>
/// One input a trace reads from, which is one tab
/// </summary>
/// <remarks>
/// <see cref="NodeId"/> is what steps carry as their source, so it is the only thing needed to route a step to the tab that shows it.
/// </remarks>
public sealed record SimulationSource(int NodeId, IteratorDefinition Definition)
{
    public SimulationSourceKind VisualType => Definition switch
    {
        AllocationScanDefinition => SimulationSourceKind.Allocation,
        HeapFetchDefinition => SimulationSourceKind.Heap,
        ColumnstoreScanDefinition or BatchToRowDefinition => SimulationSourceKind.Columnstore,
        _ => SimulationSourceKind.Index
    };

    public SimulationSourceRole Role { get; init; }

    /// <summary>
    /// The operator reading this input, which is what gives the role its meaning
    /// </summary>
    public int OperatorNodeId { get; init; }
}
