using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Mcp.Interfaces.Commands;

public interface IIndexCommands
{
    Task ZoomToPageAsync(PageAddress address, float? zoom);

    Task SelectPageAsync(PageAddress? address);

    Task SelectRowAsync(int? slot);
}