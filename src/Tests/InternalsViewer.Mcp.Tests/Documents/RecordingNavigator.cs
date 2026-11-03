using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
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

    public Task ShowObjectGraphAsync(int? block, TracePosition? position)
        => Record($"ShowObjectGraph {block?.ToString() ?? "-"} {position?.ToString() ?? "-"}");

    public Task<McpWatch> AddWatchAsync(int block, int offset, int length, string? dataType, string? label)
    {
        Calls.Add($"AddWatch {block} 0x{offset:X} {length} {dataType ?? "-"} {label ?? "-"}");

        return Task.FromResult(new McpWatch("Object", block, offset, length, dataType, label, "1", IsTemporary: true));
    }

    public Task<McpWatch[]> GetWatchesAsync() => Task.FromResult<McpWatch[]>([]);

    public Task<int> ClearWatchesAsync() => Task.FromResult(0);

    public Task<McpFunctionSignature?> ResolveSignatureAsync(CallstackFrame frame) => Task.FromResult<McpFunctionSignature?>(null);

    public TraceValueReferenceResult? References { get; set; }

    public Task<TraceValueReferenceResult?> FindReferencesAsync(ulong value)
    {
        Calls.Add($"FindReferences 0x{value:X}");

        return Task.FromResult(References);
    }

    public Task SetOptionAsync(string key, bool enabled)
        => Record($"SetOption {key} {enabled}");

    private Task Record(string call)
    {
        Calls.Add(call);

        return Task.CompletedTask;
    }
}