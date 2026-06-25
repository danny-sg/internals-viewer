using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Connections.File;

public sealed class FileConnectionType(IPageReader pageReader, string name) : IConnectionType
{
    public IPageReader PageReader { get; } = pageReader;

    public string Name { get; set; } = name;

    public string Identifier => "File";

    public string GetConnectionString()
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync() => PageReader.DisposeAsync();
}