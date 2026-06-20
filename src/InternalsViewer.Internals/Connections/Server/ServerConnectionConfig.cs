namespace InternalsViewer.Internals.Connections.Server;

public sealed class ServerConnectionConfig : ConnectionTypeConfig
{
    public string ConnectionString { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}