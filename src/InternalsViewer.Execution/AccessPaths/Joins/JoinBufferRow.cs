using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Joins;

/// <summary>
/// A row a join is holding, and what it has decided about it
/// </summary>
/// <remarks>
/// The state is set from the comparison that judges the row, which is the point sort order or the loop structure has proven whether a
/// partner can still be found.
/// </remarks>
public readonly record struct JoinBufferRow(IRecord Record, JoinRowState State)
{
    public bool IsMatched => State == JoinRowState.Matched;
}
