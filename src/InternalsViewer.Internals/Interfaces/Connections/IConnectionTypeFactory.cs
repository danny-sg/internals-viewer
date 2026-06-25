using InternalsViewer.Internals.Connections;

namespace InternalsViewer.Internals.Interfaces.Connections;

/// <summary>
/// Non-generic base interface for connection type factories, used for DI enumeration by identifier.
/// </summary>
public interface IConnectionTypeFactory
{
    string Identifier { get; }
}

/// <summary>
/// Typed factory for creating a connection of a specific configuration type.
/// </summary>
public interface IConnectionTypeFactory<out TConfig> : IConnectionTypeFactory
    where TConfig : ConnectionTypeConfig
{
    IConnectionType Create(Action<TConfig> configDelegate);
}
