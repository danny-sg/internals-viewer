using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Interfaces.Connections;

public interface IConnectionType : IAsyncDisposable
{
    IPageReader PageReader { get; }

    string Identifier { get; }
    
    string Name { get; set; }

    string GetConnectionString();
}