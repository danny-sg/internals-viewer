using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Internals.Connections.File;

public class FileConnectionFactory : IConnectionTypeFactory<FileConnectionTypeConfig>
{
    public string Identifier => "File";

    public static IConnectionType GetConnection(FileConnectionTypeConfig config)
    {
        return new FileConnectionType(new DataFilePageReader(config.Filename));
    }

    public static IConnectionType Create(Action<FileConnectionTypeConfig> configDelegate)
    {
        var config = new FileConnectionTypeConfig();

        configDelegate(config);

        return new FileConnectionType(new DataFilePageReader(config.Filename));
    }
}