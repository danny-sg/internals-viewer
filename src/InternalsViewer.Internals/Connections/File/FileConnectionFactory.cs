using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Internals.Connections.File;

public class FileConnectionFactory : IConnectionTypeFactory<FileConnectionTypeConfig>
{
    public string Identifier => "File";

    public static IConnectionType Create(Action<FileConnectionTypeConfig> configDelegate)
    {
        var config = new FileConnectionTypeConfig();

        configDelegate(config);

        var name = System.IO.Path.GetFileNameWithoutExtension(config.Filename);

        return new FileConnectionType(new DataFilePageReader(config.Filename), name);
    }
}