using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Connection.BackupFile.Connection;

public sealed class BackupConnectionType(IPageReader pageReader, string name) : IConnectionType
{
    public string Identifier => "Backup";

    public string Name { get; set; } = name;

    public IPageReader PageReader { get; } = pageReader;

    public string GetConnectionString()
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync() => PageReader.DisposeAsync();
}