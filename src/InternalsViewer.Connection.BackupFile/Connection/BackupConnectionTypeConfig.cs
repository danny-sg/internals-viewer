using InternalsViewer.Internals.Connections;

namespace InternalsViewer.Connection.BackupFile.Connection;

public sealed class BackupConnectionTypeConfig : ConnectionTypeConfig
{
    public string Filename { get; set; } = string.Empty;

    public List<string> Filenames { get; set; } = [];
}