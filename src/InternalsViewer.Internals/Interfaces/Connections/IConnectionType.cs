using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Interfaces.Connections;

public interface IConnectionType
{
    IPageReader PageReader { get; }

    string Identifier { get; }
}