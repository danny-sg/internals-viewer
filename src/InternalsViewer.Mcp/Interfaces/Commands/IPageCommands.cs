using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Models.Context.Pages;

namespace InternalsViewer.Mcp.Interfaces.Commands;

public interface IPageCommands
{
    Task GoToPageAsync(PageAddress address, int? slot);

    Task SelectSlotAsync(int slot);

    Task SelectMarkerAsync(string? name);

    Task ScrollToAsync(int offset);

    Task ShowTabAsync(PageViewTab tab);

    Task OpenIndexAsync();
}