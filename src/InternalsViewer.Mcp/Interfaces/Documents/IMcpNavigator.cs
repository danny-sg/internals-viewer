using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Mcp.Interfaces.Documents;

public interface IMcpNavigator
{
    Task ShowCallAsync(CallStackNode node, int callIndex, CancellationToken cancellationToken);

    Task ShowPageAsync(PageAddress pageAddress, ushort? slot, CancellationToken cancellationToken);

    Task ShowPlanNodeAsync(PlanNodeIdentifier identifier, CancellationToken cancellationToken);

    Task ShowEventAsync(EngineEvent engineEvent, CancellationToken cancellationToken);

    Task SetPlayheadAsync(TracePosition position, CancellationToken cancellationToken);

    Task ShowMemoryAsync(int block, ulong address, TracePosition position, CancellationToken cancellationToken);

    Task<McpSelection> GetSelectionAsync(CancellationToken cancellationToken);
}
