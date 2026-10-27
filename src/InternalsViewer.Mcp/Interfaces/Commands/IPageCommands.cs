using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Models.Context.Pages;

namespace InternalsViewer.Mcp.Interfaces.Commands;

public interface IPageCommands
{
    Task<string> GoToPageAsync(PageAddress address, int? slot);

    Task<string> SelectSlotAsync(int slot);

    Task<string> SelectMarkerAsync(string? name);

    Task<string> ScrollToAsync(int offset);

    Task<string> ShowTabAsync(PageViewTab tab);

    Task<string> OpenIndexAsync();
}