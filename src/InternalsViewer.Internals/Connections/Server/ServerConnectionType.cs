using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Internals.Connections.Server;

public class ServerConnectionType(IPageReader pageReader) : IConnectionType
{
    public string Identifier => "Server";

    public IPageReader PageReader { get; } = pageReader;
    
    public static IConnectionType GetConnection(ServerConnectionConfig config)
    {
        return new ServerConnectionType(new QueryPageReader(config.ConnectionString));
    }
}