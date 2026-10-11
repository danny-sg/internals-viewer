namespace InternalsViewer.Connection.BackupFile.Mtf.Configuration;

internal sealed class BackupConfiguration
{
    public string DatabaseName { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    public List<BackupFilegroup> Filegroups { get; } = [];

    public List<BackupFileEntry> Files { get; } = [];
}
