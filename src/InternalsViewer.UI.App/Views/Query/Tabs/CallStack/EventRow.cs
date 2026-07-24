using InternalsViewer.Query.Events;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

/// <summary>
/// An event as the subject of the scope header — what the isolated call tree below it belongs to
/// </summary>
/// <remarks>
/// A wrapper for the same reason as <see cref="OperatorRow"/>: it exists so the header can pick a template by type. An
/// event and an operator both head a segment, and only the type tells them apart.
/// </remarks>
/// <param name="Event">The event whose own stack is shown</param>
public sealed record EventRow(EngineEvent Event);
