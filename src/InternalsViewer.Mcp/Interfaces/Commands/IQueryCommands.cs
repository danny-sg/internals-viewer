using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Mcp.Interfaces.Commands;

/// <summary>
/// Commands a Query tab accepts
/// </summary>
public interface IQueryCommands
{
    Task ShowCallAsync(CallStackNode node, int callIndex);

    Task ShowPageAsync(PageAddress pageAddress, ushort? slot);

    Task ShowPlanNodeAsync(PlanNodeIdentifier identifier);

    Task ShowEventAsync(EngineEvent engineEvent);

    Task SetPlayheadAsync(TracePosition position);

    Task ShowMemoryAsync(int block, ulong address, TracePosition position);

    Task<McpFunctionSignature?> ResolveSignatureAsync(CallstackFrame frame);

    Task<string> SetOptionAsync(string key, bool enabled);
}
