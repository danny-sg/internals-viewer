using InternalsViewer.Internals.Interfaces.Connections;

namespace InternalsViewer.Internals.Connections.Backup;

public class BackupConnectionFactory : IConnectionFactory<BackupConnectionType, BackupConnectionTypeConfig>
{
    public string Identifier => "Backup";

    public BackupConnectionType GetConnection(BackupConnectionTypeConfig config)
    {
        throw new NotImplementedException();
    }
}