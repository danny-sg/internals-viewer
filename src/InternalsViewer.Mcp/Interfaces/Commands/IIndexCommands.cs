using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Mcp.Interfaces.Commands;

public interface IIndexCommands
{
    Task<string> ZoomToPageAsync(PageAddress address, float? zoom);

    Task<string> SelectPageAsync(PageAddress? address);

    Task<string> SelectRowAsync(int? slot);
}