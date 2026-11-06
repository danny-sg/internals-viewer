using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Mcp.Interfaces.Commands;

/// <summary>
/// Commands a Query tab accepts
/// </summary>
public interface IQueryCommands
{
    Task ShowCallAsync(CallTreeNode node, int callIndex);

    Task ShowPageAsync(PageAddress pageAddress, ushort? slot);

    Task ShowPlanNodeAsync(PlanNodeIdentifier identifier);

    Task ShowEventAsync(EngineEvent engineEvent);

    Task SetPlayheadAsync(TracePosition position);

    Task ShowMemoryAsync(int block, ulong address, TracePosition position);

    Task ShowObjectGraphAsync(int? block, TracePosition? position);

    Task<McpWatch> AddWatchAsync(int block, int offset, int length, string? dataType, string? label);

    Task<McpWatch[]> GetWatchesAsync();

    Task<int> ClearWatchesAsync();

    Task<McpFunctionSignature?> ResolveSignatureAsync(CallFrame frame);

    Task<TraceValueReferenceResult?> FindReferencesAsync(ulong value);

    Task SetOptionAsync(string key, bool enabled);
}
