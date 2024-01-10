using InternalsViewer.Internals.Interfaces.Connections;

namespace InternalsViewer.Internals.Connections.Backup;

public class BackupConnectionFactory : IConnectionTypeFactory<BackupConnectionTypeConfig>
{
    public string Identifier => "Backup";

    public static IConnectionType Create(Action<BackupConnectionTypeConfig> configDelegate)
    {
        var config = new BackupConnectionTypeConfig();

        configDelegate(config);

        throw new NotImplementedException();
    }
}