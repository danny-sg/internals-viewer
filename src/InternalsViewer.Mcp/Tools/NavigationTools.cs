using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Tools.Allocations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Plans.Model;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class NavigationTools(McpDocumentResolver documents, IMcpWorkspace? workspace = null)
{
    private const string DocumentDescription = "Query tab id from list_tabs; the active Query tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    private IMcpWorkspace? Workspace { get; } = workspace;

    [McpServerTool(Name = "show_call")]
    [Description("Open a call tree node in the app's Call Tree, and with a call index the arguments of that call from the call log")]
    public async Task<string> ShowCall([Description("Call tree node id")] int node,
                                       [Description("Call index within the node's function, from query_calls; omit for the node alone")]
                                       int? callIndex = null,
                                       [Description(DocumentDescription)] string? document = null,
                                       CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var target = scope.Context.Nodes.GetNode(node) ?? throw new McpException($"No call tree node {node}.");

        await scope.RequireNavigator().ShowCallAsync(target, callIndex ?? -1, cancellationToken);

        var builder = new StringBuilder();

        builder.AppendLine($"Opened in the Call Tree: {Format.NodeLine(scope.Context, target)}");

        builder.AppendLine($"  called from: {Format.CallPath(scope.Context, target, 8)}");

        if (callIndex is { } index)
        {
            builder.AppendLine($"  showing call #{index}");
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "show_page")]
    [Description("Open a page in the app, optionally with a slot selected. In a Query tab it opens in that tab's page dock, " +
                 "otherwise in a new Page tab")]
    public async Task<string> ShowPage([Description("Page address as file:page, for example 1:1048")] string page,
                                       [Description("Slot to select on the page")] int? slot = null,
                                       [Description("Tab id from list_tabs; the active tab when omitted")] string? document = null,
                                       CancellationToken cancellationToken = default)
    {
        var tab = await Documents.ResolveTabAsync(document, cancellationToken);

        var pageAddress = Parse.PageAddress(page);

        var selectedSlot = slot is { } s ? (ushort?)s : null;

        if (tab is IMcpDocument)
        {
            var scope = await Documents.ResolveAsync(tab.Id, cancellationToken);

            await scope.RequireNavigator().ShowPageAsync(pageAddress, selectedSlot, cancellationToken);
        }
        else
        {
            var database = tab.Database ?? throw new McpException($"Tab {tab.Id} has no database connection.");

            await RequireWorkspace().OpenPageAsync(database, pageAddress, selectedSlot, cancellationToken);
        }

        return slot is { } selected ? $"Opened page {pageAddress} with slot {selected} selected." : $"Opened page {pageAddress}.";
    }

    [McpServerTool(Name = "show_index")]
    [Description("Open an index in a new Index tab, from an allocation unit id or a name matched against schema.table.index")]
    public async Task<string> ShowIndex([Description("Allocation unit id, or a regex for the name; see allocations_objects")] string unit,
                                        [Description("Tab id from list_tabs; the active tab with a database when omitted")]
                                        string? tab = null,
                                        CancellationToken cancellationToken = default)
    {
        var resolved = await Documents.ResolveDatabaseTabAsync(tab, cancellationToken);

        var allocationUnit = AllocationUnitFinder.Find(resolved.Database!.AllocationUnits, unit);

        if (allocationUnit.RootPage == PageAddress.Empty)
        {
            throw new McpException($"{AllocationUnitFinder.Describe(allocationUnit)} has no root page to open as an index.");
        }

        await RequireWorkspace().OpenIndexAsync(resolved.Database!, allocationUnit.RootPage, cancellationToken);

        return $"Opened {AllocationUnitFinder.Describe(allocationUnit)} from root page {allocationUnit.RootPage} in a new Index tab.";
    }

    [McpServerTool(Name = "show_columnstore")]
    [Description("Open a columnstore index in a new Columnstore tab, from an allocation unit id or a name")]
    public async Task<string> ShowColumnstore([Description("Allocation unit id, or a regex for the name; see allocations_objects")]
                                              string unit,
                                              [Description("Tab id from list_tabs; the active tab with a database when omitted")]
                                              string? tab = null,
                                              CancellationToken cancellationToken = default)
    {
        var resolved = await Documents.ResolveDatabaseTabAsync(tab, cancellationToken);

        var allocationUnit = AllocationUnitFinder.Find(resolved.Database!.AllocationUnits, unit);

        await RequireWorkspace().OpenColumnstoreAsync(resolved.Database!, allocationUnit.AllocationUnitId, cancellationToken);

        return $"Opened {AllocationUnitFinder.Describe(allocationUnit)} in a new Columnstore tab.";
    }

    [McpServerTool(Name = "show_plan_node")]
    [Description("Select a plan operator in the app's execution plan and open its properties")]
    public async Task<string> ShowPlanNode([Description("Plan node id")] int node,
                                           [Description(DocumentDescription)] string? document = null,
                                           CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var identifier = GetPlanNodeIdentifier(scope, node);

        await scope.RequireNavigator().ShowPlanNodeAsync(identifier, cancellationToken);

        var planNode = scope.Result.ExecutionPlans.Select(p => p.NodesById.GetValueOrDefault(node)).FirstOrDefault(n => n is not null);

        return planNode is null
            ? $"Selected plan node {node}."
            : $"Selected plan node [{node}] {planNode.PhysicalOperator} ({planNode.LogicalOperator}) " +
              $"est {planNode.EstimatedRows} actual {planNode.RowsOutput} rows.";
    }

    [McpServerTool(Name = "show_event")]
    [Description("Select an event in the app's event list and timeline")]
    public async Task<string> ShowEvent([Description("Event id")] int eventId,
                                        [Description(DocumentDescription)] string? document = null,
                                        CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var engineEvent = scope.Context.Events.GetEvent(eventId) ?? throw new McpException($"No event {eventId}.");

        await scope.RequireNavigator().ShowEventAsync(engineEvent, cancellationToken);

        return $"Selected event {Format.EventLine(scope.Context, engineEvent, scope.CropStartUs)}";
    }

    [McpServerTool(Name = "set_playhead")]
    [Description("Move the Full Trace timeline playhead to a position, so the call stack and memory panes show that moment")]
    public async Task<string> SetPlayhead([Description("Trace position as hex Sequence:Steps")] string position,
                                          [Description(DocumentDescription)] string? document = null,
                                          CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        scope.RequireTrace();

        var target = Parse.Position(position);

        await scope.RequireNavigator().SetPlayheadAsync(target, cancellationToken);

        return $"Playhead moved to {Format.Position(target)}.";
    }

    [McpServerTool(Name = "show_memory")]
    [Description("Open a memory history block in the app's memory pane at a position")]
    public async Task<string> ShowMemory([Description("Memory history block number from query_memoryhistory")] int block,
                                         [Description("Address to scroll to, hex; the block's start when omitted")] string? address = null,
                                         [Description("Trace position as hex Sequence:Steps; the block's first write when omitted")]
                                         string? position = null,
                                         [Description(DocumentDescription)] string? document = null,
                                         CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var history = scope.RequireTrace().MemoryHistory;

        if (block < 0 || block >= history.Blocks.Count)
        {
            throw new McpException($"No memory history block {block}; there are {history.Blocks.Count}.");
        }

        var target = history.Blocks[block];

        var at = position is null ? target.FromPosition : Parse.Position(position);

        var scrollTo = address is null ? target.Address : Parse.Hex(address);

        await scope.RequireNavigator().ShowMemoryAsync(block, scrollTo, at, cancellationToken);

        return $"Opened memory block {block} at {Format.Hex(target.Address)} ({target.Size} bytes) at position {Format.Position(at)}.";
    }

    private IMcpWorkspace RequireWorkspace()
        => Workspace ?? throw new McpException("The app cannot open new tabs from here.");

    private static PlanNodeIdentifier GetPlanNodeIdentifier(McpDocumentScope scope, int node)
    {
        var operatorEvent = scope.Result.EngineEvents.OfType<ExecutionOperatorEvent>()
                                 .FirstOrDefault(o => o.PlanNodeIdentifier?.NodeId == node);

        if (operatorEvent?.PlanNodeIdentifier is { } identifier)
        {
            return identifier;
        }

        var plan = scope.Result.ExecutionPlans.FirstOrDefault(p => p.NodesById.ContainsKey(node))
                   ?? throw new McpException($"No plan node {node}.");

        return new PlanNodeIdentifier(plan.PlanHandleId, node);
    }
}
