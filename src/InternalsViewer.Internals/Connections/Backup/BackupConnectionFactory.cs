using InternalsViewer.Internals.Interfaces.Connections;

namespace InternalsViewer.Internals.Connections.Backup;

public class BackupConnectionFactory : IConnectionTypeFactory<BackupConnectionTypeConfig>
{
    public const string BackupIdentifier = "Backup";

    public string Identifier => BackupIdentifier;

    public IConnectionType Create(Action<BackupConnectionTypeConfig> configDelegate)
    {
        var config = new BackupConnectionTypeConfig();

        configDelegate(config);

        throw new NotImplementedException();
    }
}