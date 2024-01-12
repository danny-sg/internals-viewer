using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Readers.Pages;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Connections.Server;

public class ServerConnectionFactory : IConnectionTypeFactory<ServerConnectionConfig>
{
    public string Identifier => "Server";

    public static IConnectionType Create(Action<ServerConnectionConfig> config)
    {
        var c = new ServerConnectionConfig();

        config.Invoke(c);
        var connectionStringBuilder = new SqlConnectionStringBuilder(c.ConnectionString);

        var name = connectionStringBuilder.InitialCatalog ?? c.Name;

        return new ServerConnectionType(new QueryPageReader(c.ConnectionString), name);
    }
}