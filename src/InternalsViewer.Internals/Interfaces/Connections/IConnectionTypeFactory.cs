using InternalsViewer.Internals.Connections;

namespace InternalsViewer.Internals.Interfaces.Connections;

public interface IConnectionTypeFactory<out TConfig> where TConfig : ConnectionTypeConfig
{
    static abstract IConnectionType Create(Action<TConfig> configDelegate);
}