using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Models.Context.Allocations;

namespace InternalsViewer.Mcp.Interfaces.Commands;

public interface IAllocationsCommands
{
    Task SelectObjectAsync(string? name, bool add);

    Task<PageAddress> ScrollToAsync(string? name, PageAddress? page);

    Task SetOverlayAsync(AllocationOverlay? overlay);

    Task SetSearchAsync(string? text);
}