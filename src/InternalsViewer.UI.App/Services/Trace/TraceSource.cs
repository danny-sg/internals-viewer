using InternalsViewer.Execution.AccessPaths.Definitions;

namespace InternalsViewer.UI.App.Services.Trace;

/// <summary>
/// One input a trace reads from, which is one tab
/// </summary>
/// <remarks>
/// <see cref="NodeId"/> is what steps carry as their source, so it is the only thing needed to route a step to the tab that should show it.
/// </remarks>
public sealed record TraceSource(int NodeId, IteratorDefinition Definition)
{
    public TraceSourceKind Kind => Definition switch
    {
        AllocationScanDefinition => TraceSourceKind.Allocation,
        HeapFetchDefinition => TraceSourceKind.Heap,
        _ => TraceSourceKind.Index
    };

    /// <summary>
    /// The role this input plays in the operator that reads it, used to label the tab
    /// </summary>
    public string Role { get; init; } = string.Empty;
}
