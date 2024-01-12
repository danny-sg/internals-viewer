using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Connections.Server;

public class ServerConnectionType(IPageReader pageReader, string name) : IConnectionType
{
    public string Identifier => "Server";

    public string Name { get; set; } = name;

    public IPageReader PageReader { get; } = pageReader;
}