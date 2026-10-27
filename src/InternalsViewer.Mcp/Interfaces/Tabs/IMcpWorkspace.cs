using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Mcp.Interfaces.Tabs;

public interface IMcpWorkspace
{
    Task OpenPageAsync(DatabaseSource database, PageAddress pageAddress, ushort? slot, CancellationToken cancellationToken);

    Task OpenIndexAsync(DatabaseSource database, PageAddress rootPage, CancellationToken cancellationToken);

    Task OpenColumnstoreAsync(DatabaseSource database, long allocationUnitId, CancellationToken cancellationToken);
}
