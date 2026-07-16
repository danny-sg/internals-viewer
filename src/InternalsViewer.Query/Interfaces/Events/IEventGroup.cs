using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.Interfaces.Events;

/// <summary>
/// A consolidated event that owns the raw events it was built from
/// </summary>
public interface IEventGroup
{
    IReadOnlyList<EngineEvent> Events { get; }
}
