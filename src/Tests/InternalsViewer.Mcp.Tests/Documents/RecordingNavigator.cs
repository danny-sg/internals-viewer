using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Mcp.Tests.Documents;

internal sealed class RecordingNavigator : IQueryCommands
{
    public List<string> Calls { get; } = [];

    public McpSelection Selection { get; set; } = new();

    public Task ShowCallAsync(CallStackNode node, int callIndex)
        => Record($"ShowCall {node.Symbol} {callIndex}");

    public Task ShowPageAsync(PageAddress pageAddress, ushort? slot)
        => Record($"ShowPage {pageAddress} {slot?.ToString() ?? "-"}");

    public Task ShowPlanNodeAsync(PlanNodeIdentifier identifier)
        => Record($"ShowPlanNode {identifier.PlanHandleId}:{identifier.NodeId}");

    public Task ShowEventAsync(EngineEvent engineEvent)
        => Record($"ShowEvent {engineEvent.Name}");

    public Task SetPlayheadAsync(TracePosition position)
        => Record($"SetPlayhead {position}");

    public Task ShowMemoryAsync(int block, ulong address, TracePosition position)
        => Record($"ShowMemory {block} 0x{address:X} {position}");

    public Task<McpFunctionSignature?> ResolveSignatureAsync(CallstackFrame frame) => Task.FromResult<McpFunctionSignature?>(null);

    public Task<string> SetOptionAsync(string key, bool enabled)
    {
        Calls.Add($"SetOption {key} {enabled}");

        return Task.FromResult($"Set {key}.");
    }

    private Task Record(string call)
    {
        Calls.Add(call);

        return Task.CompletedTask;
    }
}