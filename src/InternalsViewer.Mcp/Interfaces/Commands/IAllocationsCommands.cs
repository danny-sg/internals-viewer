using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Models.Context.Allocations;

namespace InternalsViewer.Mcp.Interfaces.Commands;

public interface IAllocationsCommands
{
    Task<string> SelectObjectAsync(string? name, bool add);

    Task<string> ScrollToAsync(string? name, PageAddress? page);

    Task<string> SetOverlayAsync(AllocationOverlay? overlay);

    Task<string> SetSearchAsync(string? text);
}