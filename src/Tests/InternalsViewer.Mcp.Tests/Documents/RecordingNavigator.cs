using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Mcp.Tests.Documents;

internal sealed class RecordingNavigator : IMcpNavigator
{
    public List<string> Calls { get; } = [];

    public McpSelection Selection { get; set; } = new();

    public Task ShowCallAsync(CallStackNode node, int callIndex, CancellationToken cancellationToken)
        => Record($"ShowCall {node.Symbol} {callIndex}");

    public Task ShowPageAsync(PageAddress pageAddress, ushort? slot, CancellationToken cancellationToken)
        => Record($"ShowPage {pageAddress} {slot?.ToString() ?? "-"}");

    public Task ShowPlanNodeAsync(PlanNodeIdentifier identifier, CancellationToken cancellationToken)
        => Record($"ShowPlanNode {identifier.PlanHandleId}:{identifier.NodeId}");

    public Task ShowEventAsync(EngineEvent engineEvent, CancellationToken cancellationToken)
        => Record($"ShowEvent {engineEvent.Name}");

    public Task SetPlayheadAsync(TracePosition position, CancellationToken cancellationToken)
        => Record($"SetPlayhead {position}");

    public Task ShowMemoryAsync(int block, ulong address, TracePosition position, CancellationToken cancellationToken)
        => Record($"ShowMemory {block} 0x{address:X} {position}");

    public Task<McpSelection> GetSelectionAsync(CancellationToken cancellationToken) => Task.FromResult(Selection);

    private Task Record(string call)
    {
        Calls.Add(call);

        return Task.CompletedTask;
    }
}
