using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Connections.File;

public class FileConnectionType(IPageReader pageReader) : IConnectionType
{
    public IPageReader PageReader { get; } = pageReader;
    
    public string Identifier => "File";
}