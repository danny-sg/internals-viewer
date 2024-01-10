using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Connections.Backup;

public class BackupConnectionType(IPageReader pageReader): IConnectionType
{
    public string Identifier => "Backup";

    public IPageReader PageReader { get; } = pageReader;
}