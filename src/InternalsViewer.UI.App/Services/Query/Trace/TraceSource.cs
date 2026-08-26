using InternalsViewer.Execution.AccessPaths.Definitions;

namespace InternalsViewer.UI.App.Services.Query.Trace;

/// <summary>
/// One input a trace reads from, which is one tab
/// </summary>
/// <remarks>
/// <see cref="NodeId"/> is what steps carry as their source, so it is the only thing needed to route a step to the tab that shows it.
/// </remarks>
public sealed record TraceSource(int NodeId, IteratorDefinition Definition)
{
    public TraceSourceKind VisualType => Definition switch
    {
        AllocationScanDefinition => TraceSourceKind.Allocation,
        HeapFetchDefinition => TraceSourceKind.Heap,
        ColumnstoreScanDefinition or BatchToRowDefinition => TraceSourceKind.Columnstore,
        _ => TraceSourceKind.Index
    };

    public TraceSourceRole Role { get; init; }

    /// <summary>
    /// The operator reading this input, which is what gives the role its meaning
    /// </summary>
    public int OperatorNodeId { get; init; }
}
