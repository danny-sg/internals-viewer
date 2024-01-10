using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Interfaces.Connections;

public interface IConnectionProvider
{
    IPageReader PageReader { get; }

    string Name { get; }
}
