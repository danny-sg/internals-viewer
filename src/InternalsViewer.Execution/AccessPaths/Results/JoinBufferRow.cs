using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results;

/// <summary>
/// A row a join is holding, and whether it has been matched against the other side
/// </summary>
/// <remarks>
/// A row is matched from the comparison that finds its key on both sides until the pairing it takes part in has been emitted.
/// </remarks>
public readonly record struct JoinBufferRow(IRecord Record, bool IsMatched);
