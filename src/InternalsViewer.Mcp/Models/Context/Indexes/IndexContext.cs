using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Mcp.Tabs;

namespace InternalsViewer.Mcp.Models.Context.Indexes;

public sealed record IndexContext : AllocationUnitContext, IAreaContext
{
    public static McpArea Area => McpArea.Index;

    public IReadOnlyList<IndexNode> Nodes { get; init; } = [];

    public PageAddress RootPage { get; init; }

    public IndexNode? SelectedNode { get; init; }

    public int? SelectedSlot { get; init; }

    public float Zoom { get; init; } = 1;

    public IReadOnlyList<IndexRecordContext> Records { get; init; } = [];
}
