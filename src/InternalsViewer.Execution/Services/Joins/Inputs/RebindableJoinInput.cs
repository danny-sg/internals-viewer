using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.Interfaces.Services.Joins.Inputs;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Services.Joins.Inputs;

/// <summary>
/// Base for a join input where a loop starts again for each row of the other side
/// </summary>
public abstract class RebindableJoinInput : JoinInput, IRebindableJoinInput
{
    public abstract bool FetchesDirectly { get; }

    public abstract Task<AccessStep> RebindAsync(DatabaseSource database,
                                                 IRecord outerRecord,
                                                 int rebindNumber,
                                                 CancellationToken cancellationToken);
}
