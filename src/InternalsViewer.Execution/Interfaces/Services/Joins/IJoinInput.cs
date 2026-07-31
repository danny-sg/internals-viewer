using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Services.Joins;

/// <summary>
/// The inner side of a loop join, re-run for each outer row
/// </summary>
/// <remarks>
/// What the outer row supplies differs by lookup: a key lookup binds index key columns, a RID lookup binds a row identifier that names
/// the page and slot outright. The loop itself is the same either way, so it works through this.
/// </remarks>
public interface ILoopInnerSide
{
    IStepService Service { get; }

    AccessStrategy? Strategy { get; }

    /// <summary>
    /// How this side finds its rows, for the announcement the join opens with
    /// </summary>
    string StartDescription { get; }

    /// <summary>
    /// Whether the row is addressed outright rather than searched for, so no comparison takes place
    /// </summary>
    bool FetchesDirectly { get; }

    /// <summary>
    /// Starts the inner side for one outer row, returning the step announcing what was bound
    /// </summary>
    Task<AccessStep> RebindAsync(DatabaseSource database, IRecord outerRecord, int rebindNumber, CancellationToken cancellationToken);
}
