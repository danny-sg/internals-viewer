using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Internals.Connections.File;

public class FileConnectionFactory : IConnectionTypeFactory<FileConnectionTypeConfig>
{
    public const string FileIdentifier = "File";

    public string Identifier => FileIdentifier;

    public IConnectionType Create(Action<FileConnectionTypeConfig> configDelegate)
    {
        var config = new FileConnectionTypeConfig();

        configDelegate(config);

        var name = System.IO.Path.GetFileNameWithoutExtension(config.Filename);

        return new FileConnectionType(new DataFilePageReader(config.Filename), name);
    }
}