using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Connections.Backup;

public class BackupConnectionType(IPageReader pageReader, string name): IConnectionType
{
    public string Identifier => "Backup";

    public string Name { get; set; } = name;

    public string GetConnectionString()
    {
        throw new NotImplementedException();
    }

    public IPageReader PageReader { get; } = pageReader;
}