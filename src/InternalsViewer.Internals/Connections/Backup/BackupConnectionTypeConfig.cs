namespace InternalsViewer.Internals.Connections.Backup;

public class BackupConnectionTypeConfig(string filename) : ConnectionTypeConfig
{
    public string Filename { get; set; } = filename;
}