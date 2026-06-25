using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Connections.Server;

public sealed class ServerConnectionType(IPageReader pageReader, string name, string connectionString) 
    : IConnectionType
{
    public string Identifier => "Server";

    public string Name { get; set; } = name;

    public IPageReader PageReader { get; } = pageReader;

    private string ConnectionString { get; } = connectionString;

    public string GetConnectionString() => ConnectionString;

    public ValueTask DisposeAsync() => PageReader.DisposeAsync();
}