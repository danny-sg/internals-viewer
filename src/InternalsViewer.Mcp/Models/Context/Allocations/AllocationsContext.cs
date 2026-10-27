using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Mcp.Tabs;

namespace InternalsViewer.Mcp.Models.Context.Allocations;

public sealed record AllocationsContext : TabContext, IAreaContext
{
    public static McpArea Area => McpArea.Allocations;

    public short FileId { get; init; }

    public string Filter { get; init; } = string.Empty;

    public string? Overlay { get; init; }

    public bool ShowsSystemObjects { get; init; }

    public IReadOnlyList<string> SelectedObjects { get; init; } = [];

    public IReadOnlyList<DatabaseFile> Files { get; init; } = [];

    public IReadOnlyDictionary<long, AllocationUnit> AllocationUnits { get; init; } = new Dictionary<long, AllocationUnit>();

    public IReadOnlyDictionary<int, PfsChain> Pfs { get; init; } = new Dictionary<int, PfsChain>();

    public IReadOnlyDictionary<int, AllocationChain> Gam { get; init; } = new Dictionary<int, AllocationChain>();

    public IReadOnlyDictionary<int, AllocationChain> SGam { get; init; } = new Dictionary<int, AllocationChain>();

    public IReadOnlyDictionary<int, AllocationChain> Dcm { get; init; } = new Dictionary<int, AllocationChain>();

    public IReadOnlyDictionary<int, AllocationChain> Bcm { get; init; } = new Dictionary<int, AllocationChain>();

    public Func<PageAddress, AllocationUnit?> FindOwner { get; init; } = _ => null;
}