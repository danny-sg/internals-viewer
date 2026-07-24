using InternalsViewer.Connection.BackupFile.Reader;
using InternalsViewer.Internals.Interfaces.Connections;

namespace InternalsViewer.Connection.BackupFile.Connection;

public sealed class BackupConnectionFactory : IConnectionTypeFactory<BackupConnectionTypeConfig>
{
    public const string BackupIdentifier = "Backup";

    public string Identifier => BackupIdentifier;

    public IConnectionType Create(Action<BackupConnectionTypeConfig> configDelegate)
    {
        var config = new BackupConnectionTypeConfig();

        configDelegate(config);

        var name = Path.GetFileNameWithoutExtension(config.Filename);

        return new BackupConnectionType(new BackupPageReader(config.Filename), name);
    }
}