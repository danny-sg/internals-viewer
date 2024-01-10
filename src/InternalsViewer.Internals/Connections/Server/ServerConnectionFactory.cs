using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Readers.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Internals.Connections.Server;

public class ServerConnectionFactory : IConnectionTypeFactory<ServerConnectionConfig>
{
    public string Identifier => "Server";

    public static IConnectionType GetConnection(ServerConnectionConfig config)
    {
        return new ServerConnectionType(new QueryPageReader(config.ConnectionString));
    }

    public static IConnectionType Create(Action<ServerConnectionConfig> config)
    {
        var c = new ServerConnectionConfig();

        config.Invoke(c);

        return new ServerConnectionType(new QueryPageReader(c.ConnectionString));
    }
}