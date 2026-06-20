using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Connections.Server;

public sealed class ServerConnectionType(IPageReader pageReader, string name, string connectionString) 
    : IConnectionType
{
    public string Identifier => "Server";

    public string Name { get; set; } = name;

    private string ConnectionString { get; } = connectionString;

    public string GetConnectionString()
    {
        return ConnectionString;
    }

    public IPageReader PageReader { get; } = pageReader;
}