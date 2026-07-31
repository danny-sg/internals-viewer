using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Interfaces.Services.Joins;

/// <summary>
/// A join input that is started again for each row of the other side, as the inner side of a loop join is
/// </summary>
/// <remarks>
/// What the outer row supplies differs by lookup: a key lookup binds index key columns, a RID lookup binds a row identifier that names
/// the page and slot outright. The loop itself is the same either way, so it works through this.
/// </remarks>
public interface IRebindableInput : IJoinInput
{
    /// <summary>
    /// Whether the row is addressed outright rather than searched for, so no comparison takes place
    /// </summary>
    bool FetchesDirectly { get; }

    /// <summary>
    /// Starts the input for one outer row, returning the step announcing what was bound
    /// </summary>
    Task<AccessStep> RebindAsync(DatabaseSource database, IRecord outerRecord, int rebindNumber, CancellationToken cancellationToken);
}
