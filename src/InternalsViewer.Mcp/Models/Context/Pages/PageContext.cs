using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Mcp.Tabs;
using Page = InternalsViewer.Internals.Engine.Pages.Page;

namespace InternalsViewer.Mcp.Models.Context.Pages;

public sealed record PageContext : AllocationUnitContext, IAreaContext
{
    public static McpArea Area => McpArea.Page;

    public Page? Page { get; init; }

    public PageAddress Address { get; init; }

    public int? CurrentSlot { get; init; }

    public IReadOnlyList<PageStructure> Structures { get; init; } = [];

    public string? SelectedName { get; init; }

    public string? SelectedValue { get; init; }

    public int SelectionStart { get; init; }

    public byte[]? Selection { get; init; }

    public IReadOnlyList<IRecord> Records { get; init; } = [];

    public string? View { get; init; }

    public PageViewTab Tab { get; init; }

    public IReadOnlyList<PageViewTab> AvailableTabs { get; init; } = [];

    public IReadOnlyList<string> MarkerNames { get; init; } = [];
}
